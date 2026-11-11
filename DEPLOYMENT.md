# Deploying DBTracker to IIS

This guide covers a first deployment of DBTracker on Windows Server with IIS, the first-run setup
wizard, upgrades, and the problems you are most likely to hit. Throughout, the site is called
`DBTracker`, the app pool is called `DBTracker`, and the site folder is `C:\inetpub\DBTracker`.
Substitute your own names.

The quickest route is the installer described next. Sections 1 to 5 describe the same steps done by
hand, and the rest of the guide applies either way.

## Install with Setup.exe or the MSI

There are two installer files:

- **`DBTracker-Setup.exe` (recommended).** Checks the prerequisites, lists what is missing, and after
  you confirm installs them: the IIS features DBTracker needs (including WebSocket Protocol), the .NET
  10 Hosting Bundle, and optionally SQL Server Express. Downloads come from Microsoft and are checked
  against a pinned SHA-512 before they run. It then runs `DBTracker.msi` and opens the setup wizard.
- **`DBTracker.msi`.** Installs DBTracker itself: copies the app to `C:\inetpub\DBTracker`, creates the
  `DBTracker` app pool (No Managed Code, ApplicationPoolIdentity) and site, gives the app pool identity
  Modify rights on `App_Data`, and can create the SQL Server login for it. It does not install
  prerequisites. Run on its own, it stops with a message naming exactly what is missing: IIS 10, the IIS
  WebSocket Protocol feature, or the .NET 10 Hosting Bundle (ASP.NET Core 10 runtime plus ASP.NET Core
  Module V2 version 20 or later).

Both need administrator rights. When the install finishes, the browser opens
`http://localhost:<port>/install` and the setup key file (`App_Data\setup-key.txt`) opens in your
default text editor, so you can paste the key straight into the wizard (section 6). On an upgrade the
site's home page opens instead.

### Setup.exe options

The Options page has the install folder, the port, the SQL Server for the login, and **Install SQL
Server Express**, which only appears when no local SQL Server instance exists. From the command line:

```powershell
DBTracker-Setup.exe /quiet SitePort=8200 InstallFolder="D:\Sites\DBTracker" SqlLoginServer=. LaunchWizard=0
DBTracker-Setup.exe /quiet InstallSqlExpress=1
```

`/quiet` installs without prompts, and `/norestart` suppresses the restart prompt. Turning on IIS
features, the Hosting Bundle or SQL Server Express sometimes needs a restart (exit code 3010); Setup.exe
offers it at the end and never forces it. Logs: `%TEMP%\DBTracker-Setup_*.log` for the bundle and
`%TEMP%\DBTracker-SetupHelper.log` for its prerequisite helper.

### MSI properties

| Property | Default | Meaning |
|---|---|---|
| `INSTALLFOLDER` | `C:\inetpub\DBTracker` | Site folder. |
| `SITE_NAME` | `DBTracker` | IIS site name. |
| `APPPOOL_NAME` | `DBTracker` | IIS app pool name; its identity is `IIS APPPOOL\<name>`. |
| `SITE_PORT` | `8150` | HTTP port. Architonix app services use the 8xxx and 9xxx bands. The install stops if another IIS site already uses the port. |
| `SQL_SERVER` | empty | SQL Server to create the app pool's login on, for example `.` or `.\SQLEXPRESS`. |
| `SQL_GRANT_LOGIN` | empty | `1` creates the login and adds it to `dbcreator`. |
| `LAUNCH_WIZARD` | `1` | `0` skips opening the browser at the end. |
| `OPEN_SETUP_KEY` | `1` | `0` skips opening the setup key file. |
| `REMOVE_DATA` | empty | On uninstall only: `1` also deletes `App_Data`. |

Names may use letters, digits, spaces, dots, hyphens and underscores. The installer remembers the
folder, site, pool and port (in `HKLM\SOFTWARE\Architonix Labs\DBTracker`), so repairs, upgrades and
uninstall act on the same ones; to move the site to another port later, change the binding in IIS
Manager.

```powershell
# Interactive, with a verbose log
msiexec /i DBTracker.msi /l*v install.log

# Unattended, on another port, creating the SQL login on the local default instance
msiexec /i DBTracker.msi /qn /l*v install.log SITE_PORT=8150 SQL_SERVER=. SQL_GRANT_LOGIN=1

# Uninstall, keeping App_Data (the default)
msiexec /x DBTracker.msi /qn /l*v uninstall.log

# Uninstall and delete App_Data (saved configuration, encryption keys, setup key)
msiexec /x DBTracker.msi /qn /l*v uninstall.log REMOVE_DATA=1
```

The SQL login is created with your Windows credentials, so you need permission to create logins on
that server. For SQL Server on the same machine the login is `IIS APPPOOL\<pool>`; for a server on
another machine it is the IIS server's computer account (`DOMAIN\SERVER$`), which needs the IIS server
to be in a domain. If creating the login fails, the installer shows a warning, logs it, and finishes
the install anyway; create the login by hand with the statements in section 5.

**Upgrades.** Run the newer `DBTracker-Setup.exe` or `DBTracker.msi`. It stops the app pool, replaces
the program files, keeps `App_Data`, and starts the pool again; pending database migrations apply on
the next request. Installing an older version over a newer one is blocked.

**Uninstall** (Settings > Apps, or `msiexec /x`) removes the site, the app pool and the program files.
`App_Data` stays unless you pass `REMOVE_DATA=1`. The database is never touched. Prerequisites that
Setup.exe installed (IIS features, the Hosting Bundle, SQL Server Express) also stay installed.

**Known gap.** If the .NET Hosting Bundle was installed before IIS, the ASP.NET Core Module files exist
but IIS does not know about them, and neither installer can detect that. The site then fails with HTTP
500.19. Run the Hosting Bundle installer again, choose Repair, and run `iisreset`.

### Build the installer

On a Windows build machine with the .NET 10 SDK, from the repository root:

```powershell
.\installer\build.ps1 -Version 1.0.0
```

It publishes the app, builds `installer\out\DBTracker.msi`, refreshes the pinned prerequisite
downloads and hashes from Microsoft's release metadata, and builds `installer\out\DBTracker-Setup.exe`.
`-SkipBundle` builds only the MSI. The installer builds with WiX Toolset 5.0.2 (MS-RL, restored from
NuGet, nothing to install) and is deliberately not on WiX 6 or later, which ship under the Open Source
Maintenance Fee EULA. Windows Installer's ICE validation needs an elevated prompt, so a normal build
skips it; run `.\installer\build.ps1 -Validate` from an elevated prompt to include it.

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

**Setup.exe fails right away with 0x800f0831 or 0x800f080c.** This copy of Windows does not include
IIS: DISM reports every IIS feature name as unknown (`%TEMP%\DBTracker-SetupHelper.log` says so). It
happens on Windows Home and inside Windows Sandbox. Install on Windows Server, or Windows 10/11 Pro,
Enterprise or Education. For a throwaway test machine use a Hyper-V VM (see
`installer\test\Start-HyperVTest.ps1`), not Windows Sandbox.

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
