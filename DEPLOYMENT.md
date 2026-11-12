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
  against a pinned SHA-512 before they run. The Hosting Bundle's hash comes from Microsoft's release
  metadata. SQL Server Express is Microsoft's small web installer, whose hash is recorded at build time
  after its Microsoft signature is checked; that installer then downloads the SQL Server media itself.
  It then runs `DBTracker.msi` and opens the setup wizard.
- **`DBTracker.msi`.** Installs DBTracker itself: copies the app to `C:\inetpub\DBTracker`, creates the
  `DBTracker` app pool (No Managed Code, ApplicationPoolIdentity) and site, gives the app pool identity
  Modify rights on `App_Data`, and can create the SQL Server login for it. It does not install
  prerequisites. Run on its own, it stops with a message naming exactly what is missing: IIS 10, the IIS
  WebSocket Protocol feature, or the .NET 10 Hosting Bundle (ASP.NET Core 10 runtime plus ASP.NET Core
  Module V2 version 20 or later).

Both need administrator rights. When the install finishes, the browser opens
`http://localhost:<port>/install` (with HTTPS, `https://<certificate name>[:<HTTPS port>]/install`)
and the setup key file (`App_Data\setup-key.txt`) opens in your
default text editor, so you can paste the key straight into the wizard (section 6). On an upgrade the
site's home page opens instead.

### DBTracker Tray

Both installers also install **DBTracker Tray** to `C:\Program Files\Architonix Labs\DBTracker Tray`
(outside the site folder), add a "DBTracker Tray" Start menu shortcut, and start it for every user
at sign-in. It starts for the installing user as soon as the install finishes. It runs as the signed-in
user, needs nothing beyond the .NET Framework 4.6.2 that ships with Windows Server 2016 and later, and
reads the site's port and folders from `HKLM\SOFTWARE\Architonix Labs\DBTracker`. When the installer
set up HTTPS (`SiteScheme` is `https` there), the tray polls `https://localhost:<HttpsPort>` and opens
browser links at `https://<CertHost>`; otherwise it uses `http://localhost:<SitePort>`.

The tray icon is green when DBTracker answers its health check, amber when it is running but needs
attention (setup not finished, or the database unreachable) and red when it does not answer. The
tooltip says which, and a notification appears when the state changes and stays changed. Its menu:

- **Open DBTracker**, or **Open the setup wizard** while setup is pending. Clicking the icon does the same.
- **Backups**: failed, overdue and never-backed-up counts, with a notification when an instance becomes
  newly at risk. Choose **Connect to DBTracker...** and paste a personal access token created at
  `/account/tokens`. The token is stored for that Windows user only, encrypted with Windows DPAPI, in
  `%LOCALAPPDATA%\DBTracker\Tray`. **Disconnect** deletes it.
- **Admin**: start, stop or restart the app pool, start or stop the site, and copy the setup key while
  setup is pending. Each shows a Windows administrator prompt, then reports the result.
- **Folders**: the install folder, `App_Data`, the site's IIS log folder (administrators get the
  site's own `W3SVC<id>` folder; others get the parent log folder) and this guide.
- **Recent errors**: the last 20 Application event log errors and warnings that mention this site.
- **Start with Windows**: turn automatic start off or on for your own account.

Install and upgrade close any running copy first, and uninstall removes the tray along with the site.
If Windows shows an unknown-publisher warning or a long first start on Windows 11 with Smart App
Control on, the installer and tray app are not code-signed; sign them before distributing widely.

### Setup.exe options

The Options page has the install folder, the port, the SQL Server for the login, **Install SQL
Server Express** (only when no local SQL Server instance exists), and an optional **HTTPS
certificate thumbprint** and **HTTPS port** (see [HTTPS](#https) below). From the command line:

```powershell
DBTracker-Setup.exe /quiet SitePort=8200 InstallFolder="D:\Sites\DBTracker" SqlLoginServer=. LaunchWizard=0
DBTracker-Setup.exe /quiet InstallSqlExpress=1
DBTracker-Setup.exe /quiet CertThumbprint=0123456789ABCDEF0123456789ABCDEF01234567 HttpsPort=443
DBTracker-Setup.exe /uninstall /quiet RemoveData=1
```

The uninstall page (Settings > Apps > DBTracker Setup > Uninstall, or running Setup.exe again) has
**Also delete DBTracker's data in App_Data**, off by default. Ticking it is the same as
`RemoveData=1`; see **Uninstall** below for exactly what it deletes. A `/quiet` uninstall without
`RemoveData=1` keeps the data, as before.

When a local SQL Server already exists, Setup doesn't create the app pool's SQL login unless you name
the server with `SqlLoginServer` (or fill in the field on the Options page).

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
| `LAUNCH_TRAY` | `1` | `0` skips starting DBTracker Tray for the installing user at the end. Setup.exe variable: `LaunchTray`. |
| `OPEN_SETUP_KEY` | `1` | `0` skips opening the setup key file. |
| `REMOVE_DATA` | empty | On uninstall only: `1` also deletes `App_Data`. Setup.exe variable: `RemoveData`, or the checkbox on its uninstall page. |
| `CERT_THUMBPRINT` | empty | Thumbprint of a certificate in `LocalMachine\My` with its private key. Adds an HTTPS binding (see [HTTPS](#https)). `none` turns HTTPS off on an upgrade or repair. Setup.exe variable: `CertThumbprint`. |
| `HTTPS_PORT` | `443` | HTTPS port, used only with a certificate. Must differ from `SITE_PORT`. Setup.exe variable: `HttpsPort`. |

Names may use letters, digits, spaces, dots, hyphens and underscores. The installer remembers the
folder, site, pool and port (in `HKLM\SOFTWARE\Architonix Labs\DBTracker`), so repairs, upgrades and
uninstall act on the same ones; to move the site to another port later, change the binding in IIS
Manager. It also records `SiteScheme` (`http` or `https`), `HttpsPort`, `CertThumbprint` and
`CertHost` there, for the tray and for the next upgrade.

```powershell
# Interactive, with a verbose log
msiexec /i DBTracker.msi /l*v install.log

# Unattended, on another port, creating the SQL login on the local default instance
msiexec /i DBTracker.msi /qn /l*v install.log SITE_PORT=8150 SQL_SERVER=. SQL_GRANT_LOGIN=1

# Uninstall, keeping App_Data (the default; same as Settings > Apps)
DBTracker-Setup.exe /uninstall /quiet

# Uninstall and delete App_Data (saved configuration, encryption keys, setup key)
DBTracker-Setup.exe /uninstall /quiet RemoveData=1
```

### HTTPS

Give the installer a certificate thumbprint and it serves DBTracker over HTTPS as well as HTTP:

1. Import the certificate, with its private key, into **Local Computer > Personal**
   (`Cert:\LocalMachine\My`). Copy its **Thumbprint** from the certificate's Details tab; spaces,
   colons and the invisible character the Windows dialog adds when copying are ignored.
2. Install with `CertThumbprint=<thumbprint>` (Setup.exe, or the Options page field) or
   `CERT_THUMBPRINT=<thumbprint>` (MSI). Add `HttpsPort=<port>` or `HTTPS_PORT=<port>` for a port
   other than 443.

Before anything changes, the installer checks that the certificate is in `LocalMachine\My`, has a
private key, is within its validity dates and allows Server Authentication, and that the HTTPS port
is free and differs from the HTTP port. If a check fails, it stops with a message saying which,
before DBTracker itself is installed or changed (prerequisites Setup.exe already added stay). Otherwise it adds an `https` binding on `*:<port>` to the site and binds the
certificate to `0.0.0.0:<port>` in http.sys (`netsh http add sslcert`).

- **The HTTP binding stays.** Browsers that reach it are redirected to HTTPS. Don't open the HTTP port
  in the firewall if other machines should only use HTTPS.
- **The tray switches to HTTPS.** It polls `https://localhost:<HTTPS port>`. The certificate is
  usually issued to the server's real name, not `localhost`, so the tray accepts a certificate that is
  in this computer's own `LocalMachine\My` store; any other certificate must pass the normal Windows
  checks. Browser links from the tray use the certificate's DNS name (`CertHost`), or `localhost` for
  a wildcard certificate.
- **Upgrades and repairs keep it.** Without `CertThumbprint` they reuse the recorded certificate and
  port, and check them again. After renewing the certificate, pass the new thumbprint on the next
  upgrade, or run `DBTracker-Setup.exe /repair CertThumbprint=<new thumbprint>`. If the old
  certificate is gone, the upgrade stops and says so; it never falls back to HTTP on its own.
  `CertThumbprint=none` turns HTTPS off and removes the binding.
- **Uninstall removes it.** The site goes with its bindings, and the installer deletes the http.sys
  certificate binding for the HTTPS port. The certificate stays in the store.

You can still add the binding yourself in IIS Manager instead (section 3). The tray then keeps
polling `http://localhost:<port>`, which the app answers over HTTP only from the same machine.

### Uninstalling with Setup.exe

If you installed with `DBTracker-Setup.exe`, uninstall with it too (or from Settings > Apps, where it
appears as "DBTracker Setup"). Running `msiexec /x` on the hidden MSI underneath works, but leaves a
stale "DBTracker Setup" entry in Settings > Apps. `msiexec /x DBTracker.msi REMOVE_DATA=1` is only
for machines where the MSI was installed on its own.

The SQL login is created with your Windows credentials, so you need permission to create logins on
that server. For SQL Server on the same machine the login is `IIS APPPOOL\<pool>`; for a server on
another machine it is the IIS server's computer account (`DOMAIN\SERVER$`), which needs the IIS server
to be in a domain. If creating the login fails, the installer shows a warning, logs it, and finishes
the install anyway; create the login by hand with the statements in section 5.

**Upgrades.** Run the newer `DBTracker-Setup.exe` or `DBTracker.msi`. It stops the app pool, replaces
the program files, keeps `App_Data`, and starts the pool again; pending database migrations apply on
the next request. Installing an older version over a newer one is blocked.

**Uninstall** (Settings > Apps, or `DBTracker-Setup.exe /uninstall`) removes the site, the app pool,
the HTTPS certificate binding if the installer made one, and the program files. `App_Data` stays
unless you tick **Also delete DBTracker's data in App_Data** on the uninstall page or pass
`RemoveData=1`. Then the installer deletes the `App_Data` folder inside the site folder, and the
site folder once it is empty. `App_Data` holds the saved configuration `dbtracker.config.json`
(database connection, mail and other settings, with secrets encrypted), the setup key
`setup-key.txt`, and the Data Protection keys in `keys\` that decrypt those secrets and sign-in
cookies. A reinstall afterwards gets a new setup key and starts the setup wizard again. A data folder
set with `DBTRACKER_DATA_DIR` outside the site folder is not deleted. The database is never touched, and neither is the SQL login the installer created for
the app pool: a DBTracker database may still have a user mapped to it. To remove it when you are done
with DBTracker for good, run `DROP LOGIN [IIS APPPOOL\DBTracker];` on that SQL Server. Prerequisites
that Setup.exe installed (IIS features, the Hosting Bundle, SQL Server Express) also stay installed.

**Tray access tokens survive uninstall.** Each Windows user who connected the tray keeps their encrypted
token file in `%LOCALAPPDATA%\DBTracker\Tray` and their preferences under
`HKCU\Software\Architonix Labs\DBTracker Tray`, because an installer cannot reliably reach other users'
profiles. Only that Windows user can decrypt the file, and the token expires (90 days by default), but
it stays valid on the server until then. Before or after uninstalling, revoke each tray token at
`/account/tokens` (an Admin deactivating the account also revokes them), then delete the folder if
you want no trace left. If the server is gone for good, deleting the folder is enough.

**Known gap.** If the .NET Hosting Bundle was installed before IIS, the ASP.NET Core Module files exist
but IIS does not know about them, and neither installer can detect that. The site then fails with HTTP
500.19. Run the Hosting Bundle installer again, choose Repair, and run `iisreset`.

### Build the installer

On a Windows build machine with the .NET 10 SDK, from the repository root:

```powershell
.\installer\build.ps1 -Version 1.0.0
```

It publishes the app, builds `installer\out\DBTracker.msi`, refreshes the pinned prerequisite
downloads and hashes (the Hosting Bundle from Microsoft's release metadata, the SQL Server Express web
installer from the signed file it downloads), builds `installer\out\DBTracker-Setup.exe`, and writes
`installer\out\SHA256SUMS.txt` for the release page.
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

   The MSI creates an HTTP binding, and an HTTPS one too when you give it a certificate thumbprint
   (see [HTTPS](#https)). To add HTTPS by hand instead, add an `https` binding with your certificate
   to the `DBTracker` site in IIS Manager.
   - **Keep the HTTP binding.** Browsers that reach it are redirected to HTTPS.
   - **The tray keeps working.** It polls `http://localhost:<port>`, and the app answers its
     `/healthz` and `/api/tray` requests over HTTP only when they come from the same machine, so its
     token never crosses the network.
   - **Don't open the HTTP port** in the firewall if you want HTTPS-only access from other machines.

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

**Passkeys.** Users can add passkeys on **Passkeys** (`/account/passkeys`, in the account links under
their name) and then sign in with a fingerprint, face, screen lock or security key. Passwords keep
working, and a deactivated account can't sign in with a passkey either.
- **HTTPS is required.** Browsers only offer passkeys on `https://` pages, or on `http://localhost`
  for local testing. Over plain HTTP from another machine the passkey options are hidden and users
  sign in with their password.
- **`Passkeys:ServerDomain`** is the WebAuthn relying party ID: the domain each passkey is tied to.
  Leave it empty (the default) to use the host name in the browser's address bar. Set it, in
  `appsettings.json` or as the environment variable `Passkeys__ServerDomain`, when users reach the site
  under more than one host name, for example `dbtracker.contoso.com`. It must be the site's host name
  or a parent domain of it, such as `contoso.com`, never a URL, port or IP address.
- **Choose the domain before users add passkeys.** A passkey only works for the domain it was created
  under. If the host name or `Passkeys:ServerDomain` changes later, every existing passkey stops
  working and users must sign in with their password and add a new one.
- **Behind a reverse proxy,** the app must see the public `https` scheme and host (IIS in-process
  hosting does). If the proxy hides them, passkey checks fail because the browser's origin doesn't match.
- Passkeys are stored in the `AspNetUserPasskeys` table, which is backed up with the database. They
  hold public keys only and aren't included in any export.

**Central log analytics.** Architonix apps send their web logs to the shared GoAccess server, which
reads `/srv/weblogs/<app>/` on 192.168.1.8. Because DBTracker runs under IIS on Windows,
`tools\Ship-IisLogs.ps1` copies the site's IIS W3C logs to `ram@192.168.1.8:/srv/weblogs/dbtracker/`
with `scp`, sending only files that are new or changed since its last run.
- **Schedule:** run it from Task Scheduler, every 15 minutes for example, as an administrator account,
  since reading IIS configuration needs administrator rights.
- **SSH key:** the account needs an SSH key that the server accepts. Nothing prompts for a password.
- **Check first:** `-WhatIf` shows what it would send.
- **GoAccess format:** use the W3C log format for this folder.

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
