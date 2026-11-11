# Deploying DBTracker to IIS

This guide covers a first deployment of DBTracker on Windows Server with IIS, the first-run setup
wizard, upgrades, and the problems you are most likely to hit. Throughout, the site is called
`DBTracker`, the app pool is called `DBTracker`, and the site folder is `C:\inetpub\DBTracker`.
Substitute your own names.

## 1. Prerequisites

On the IIS server:

- **IIS** with the **WebSocket Protocol** feature installed (Server Manager > Add Roles and Features >
  Web Server (IIS) > Web Server > Application Development > WebSocket Protocol). DBTracker is a Blazor
  Server app: every page keeps a live connection to the server, and IIS only passes WebSockets through
  when this feature is installed.
- The **.NET 10 Hosting Bundle** (ASP.NET Core Runtime 10.x, Windows Hosting Bundle). It installs the
  .NET runtime and the ASP.NET Core Module that IIS uses to run the app. Install IIS first; if you
  installed the bundle before IIS, run the bundle installer again and choose Repair. Restart IIS
  afterwards (`iisreset` from an elevated prompt).

A database server reachable from the IIS server:

- SQL Server, SQL Server Express, or Azure SQL Database. DBTracker uses SQL Server only.

## 2. Publish the app

On a build machine with the .NET 10 SDK, from the repository root:

```powershell
dotnet publish src/DBTracker.Web -p:PublishProfile=FolderProfile
```

The output lands in `src\DBTracker.Web\bin\Release\net10.0\publish\`. It is framework-dependent, so
the server needs the Hosting Bundle from step 1. Copy the contents of that folder to
`C:\inetpub\DBTracker` on the server.

The published `web.config` runs the app in-process with `ASPNETCORE_ENVIRONMENT=Production`. Do not
store secrets in it: the setup wizard keeps them encrypted in `App_Data` (see step 6).

## 3. Create the app pool and site

In IIS Manager:

1. **Application Pools > Add Application Pool.** Name: `DBTracker`. .NET CLR version: **No Managed
   Code**. Managed pipeline mode: Integrated. Leave Identity as **ApplicationPoolIdentity** unless you
   want a domain service account (see step 8 for why you might).
2. **Sites > Add Website.** Site name: `DBTracker`. Application pool: `DBTracker`. Physical path:
   `C:\inetpub\DBTracker`.
3. **Binding.** Pick a port that is free on the server. Architonix app services use the 8xxx and 9xxx
   port bands, so choose one there, for example `https` on port `<PORT>`. Use HTTPS with a real
   certificate for anything beyond a test box: the app handles sign-in cookies and, once configured,
   database and mail credentials.

With ApplicationPoolIdentity the pool runs as the virtual account `IIS APPPOOL\DBTracker`. The next
steps grant that account what it needs.

## 4. Let the app write its data folder

DBTracker stores its setup key, its saved configuration and its encryption keys in `App_Data` inside
the site folder. Create it and give the app pool identity Modify rights:

```powershell
New-Item -ItemType Directory -Force C:\inetpub\DBTracker\App_Data
icacls "C:\inetpub\DBTracker\App_Data" /grant "IIS APPPOOL\DBTracker:(OI)(CI)M"
```

To keep this data outside the site folder (recommended if you redeploy by wiping the folder), set the
environment variable `DBTRACKER_DATA_DIR` to another path, for example `D:\DBTrackerData`, and grant
the same Modify right there. You can set it in `web.config` under `<environmentVariables>`, but
`web.config` is replaced on every deploy, so a machine-level environment variable is easier to keep.

## 5. Give the app pool identity access to SQL Server

The wizard recommends Windows authentication as the app pool identity, so no database password is
stored anywhere. Run this on the SQL Server as a sysadmin.

If SQL Server is on the **same machine** as IIS, the login is the app pool account:

```sql
CREATE LOGIN [IIS APPPOOL\DBTracker] FROM WINDOWS;
```

If SQL Server is on **another machine**, ApplicationPoolIdentity reaches it as the IIS server's
computer account, so create the login for that instead (replace the domain and server name):

```sql
CREATE LOGIN [CONTOSO\IISSERVER01$] FROM WINDOWS;
```

Then choose one of these:

- **Let the wizard create the database.** Grant the login the right to create databases. The login
  that creates a database becomes its owner, so nothing else is needed:

  ```sql
  ALTER SERVER ROLE dbcreator ADD MEMBER [IIS APPPOOL\DBTracker];
  ```

- **Create the database yourself** and make the login its owner-level user. DBTracker applies schema
  migrations at startup, on first install and on every upgrade, so it needs rights to change the
  schema:

  ```sql
  CREATE DATABASE DBTracker;
  GO
  USE DBTracker;
  CREATE USER [IIS APPPOOL\DBTracker] FOR LOGIN [IIS APPPOOL\DBTracker];
  ALTER ROLE db_owner ADD MEMBER [IIS APPPOOL\DBTracker];
  ```

If you cannot use Windows authentication (Azure SQL Database, or a server outside the domain), create
a SQL login instead and enter it in the wizard. The wizard encrypts the password before saving it.

## 6. Run the setup wizard

1. Browse to the site, for example `https://<server>:<PORT>/`. Until setup is finished, every page
   redirects to `https://<server>:<PORT>/install`.
2. **Setup key.** On first start the app writes a one-time key to `App_Data\setup-key.txt` in the site
   folder (or in `DBTRACKER_DATA_DIR` if you set it) and also writes it to the app log as a warning.
   Open the file on the server and paste the key into the wizard. It stops anyone else who reaches
   the site first from configuring it. The key is deleted when setup completes.
3. **Database.** Enter the SQL Server name and a database name, choose Windows authentication (as the
   app pool identity) or a SQL login, and set the encryption options for your server. Use **Test
   connection** first. If the database does not exist, the wizard creates it (this needs `dbcreator`,
   see step 5). If it is an existing DBTracker database, the wizard keeps its data and applies any
   pending migrations. Nothing is dropped.
4. **Create tables.** The wizard applies the migrations and shows progress.
5. **Sample data (optional).** Off by default. It loads sample instances and backup records plus five
   demo accounts that share a password published in this repository. Use it only for evaluation,
   never on a production server.
6. **Admin account.** Always required. This is the first real account; use it to sign in afterwards.
7. **Email (optional).** ArxMail is the default (`https://mail.architonixlabs.com/v1/submit`), SMTP is
   available, or choose none and add it later. Use the test send to confirm it works.
8. **Backup-share check (optional).** Enter a backup folder or UNC path. The wizard reports which
   Windows identity the app is running as and whether that identity can read the path.
9. **Finish.** The wizard saves the configuration to `App_Data\dbtracker.config.json` (secrets such as
   a SQL or SMTP password are encrypted with ASP.NET Data Protection, using keys stored in
   `App_Data\keys`), records the setup in the audit log, and takes you to the login page.

After setup, `/install` is closed.

## 7. After setup

Sign in with the Admin account from the wizard. The Admin **Setup** page (`/setup`) lists anything
still unconfigured, such as scanning, alerts or the report distribution list.

If you loaded sample data, deactivate the demo accounts from the **Users** page, or reinstall without
sample data, before real users rely on the site.

## 8. Backup-share permissions

DBTracker reads backup files to discover evidence that backups ran. The app pool identity needs read
permission on each backup location, on both the share and the NTFS folder:

- **Local folder on the IIS server:** grant `IIS APPPOOL\DBTracker` read access.
- **UNC share on another machine:** ApplicationPoolIdentity reaches the network as the IIS server's
  computer account, so grant read access to `CONTOSO\IISSERVER01$`. If you would rather not grant the
  computer account, run the app pool as a domain service account and grant that account instead. Use
  the same account for the SQL login in step 5.

The wizard's backup-share check shows the identity in use and whether it can read the path. After
setup, a failed scan shows its error on the instance page.

## 9. Upgrades

1. Back up the DBTracker database.
2. Take the site offline: put a file named `app_offline.htm` in the site root. The ASP.NET Core Module
   stops the app and serves that file while it exists. (Stopping the app pool also works.)
3. Copy the new publish output over the site folder. Do **not** delete `App_Data`: it holds the saved
   configuration and the encryption keys for its secrets.
4. Remove `app_offline.htm`. On the next request the app starts and applies any pending database
   migrations.

## 10. Running the wizard again

To reconfigure from scratch (for example, to point at a different database):

1. Stop the site.
2. Delete `App_Data\dbtracker.config.json`.
3. Start the site. A new setup key is generated in `App_Data\setup-key.txt`, and `/install` opens again.

Keep `App_Data\keys` unless you also intend to re-enter every saved secret.

## 11. Troubleshooting

**HTTP 500.19 (configuration error).** IIS cannot read `web.config`, usually because the ASP.NET Core
Module is not installed. Install, or repair, the .NET 10 Hosting Bundle and run `iisreset`. Also check
that the app pool identity can read the site folder.

**HTTP 500.30 (in-process start failure).** The app crashed while starting. Check Event Viewer >
Windows Logs > Application for entries from the IIS AspNetCore Module, then turn on the stdout log
(below). Common causes: `App_Data` is not writable by the app pool identity (step 4), the database
saved in the configuration is unreachable, or the server is missing the .NET 10 runtime.

**Pages load but buttons do nothing, or a "reconnecting" message keeps appearing.** The live
connection cannot be established. Confirm the WebSocket Protocol feature is installed (step 1). The
browser's developer console shows a failed WebSocket connection to `/_blazor` when this is the cause.
Proxies and load balancers in front of IIS must also allow WebSockets.

**Stdout log.** Edit `web.config` in the site folder: set `stdoutLogEnabled="true"`, create a `logs`
folder in the site root, grant the app pool identity Modify on it, recycle the app pool and reproduce
the problem. Logs appear as `logs\stdout_*.log`. Set it back to `false` when you are done; these logs
are not rotated and keep growing.

**Where is the setup key?** `App_Data\setup-key.txt` under the site folder, or under
`DBTRACKER_DATA_DIR` if set. If the file is missing and `/install` still asks for a key, check that the
app pool identity can write to that folder, then recycle the app pool.
