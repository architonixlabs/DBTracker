# DBTracker setup guide

This guide gets DBTracker running on a Windows server from the release download. For every option,
manual installs and troubleshooting, see [DEPLOYMENT.md](DEPLOYMENT.md).

## 1. What you need

- **Windows Server 2016 or later**, or Windows 10/11 **Pro, Enterprise or Education**, 64-bit. Not
  Windows Home or Windows Sandbox (they don't include IIS).
- An **administrator account** on that machine.
- **Internet access** during setup, so Setup.exe can download what's missing from Microsoft.
- A **SQL Server** the server can reach. If there isn't one, Setup.exe can install SQL Server Express
  for you.

Tested on Windows Server 2025 Datacenter with SQL Server 2025 Express.

## 2. Download and check the files

From the [Releases page](https://github.com/architonixlabs/DBTracker/releases), download
**DBTracker-Setup.exe** (recommended) and **SHA256SUMS.txt**. Check the download in PowerShell:

```powershell
$expected = (Select-String -Path .\SHA256SUMS.txt -Pattern 'DBTracker-Setup.exe').Line.Split(' ')[0]
(Get-FileHash .\DBTracker-Setup.exe -Algorithm SHA256).Hash -eq $expected
```

It must print `True`. SHA256SUMS.txt is written by `installer\build.ps1` next to the installers, and
lists `DBTracker-Setup.exe` and `DBTracker.msi`.

## 3. Run Setup.exe

1. Right-click **DBTracker-Setup.exe** and choose **Run as administrator**.
2. The files aren't code-signed yet, so Windows may say **"Windows protected your PC"**. Choose
   **More info**, then **Run anyway**.
3. Choose **Options** to check the **Site folder** (default `C:\inetpub\DBTracker`) and **Site port**
   (default `8150`). Then one of:
   - SQL Server already on this machine: enter it in the field that asks which server to give the app
     pool a login on (`.` for the default instance, `.\NAME` for a named one), so DBTracker can sign in
     with Windows authentication.
   - No SQL Server found: tick **Also install SQL Server Express on this computer**, or leave it off
     and enter an existing server in the wizard later.
4. Choose **Install**. Setup turns on IIS features, installs the .NET Hosting Bundle (and SQL Server
   Express if ticked), then DBTracker. If it asks to restart at the end, restart.

Unattended alternative: `DBTracker-Setup.exe /quiet SitePort=8150 SqlLoginServer=.`

## 4. Finish the setup wizard

When Setup finishes, your browser opens `http://localhost:8150/install`.

1. **Enter the setup key.** Paste the key from `C:\inetpub\DBTracker\App_Data\setup-key.txt` (Setup
   opens it in Notepad). It works once and is deleted when setup is done.
2. **Connect to SQL Server.** Server name (for example `.\SQLEXPRESS`), database name (default
   `DBTracker`), Windows authentication. Test the connection, then create the database.
3. **Create the tables.** The wizard creates them and shows each step.
4. **Sample data** (optional). Leave it unticked on a real server: it adds demo accounts with a
   published password.
5. **Create the Administrator account.** Your own email and a strong password. This is how you sign in
   from now on.
6. **Email delivery** (optional). ArxMail or SMTP for alerts and reports, with a test send. You can
   skip it.
7. **Check a backup share** (optional). Enter a share where backup files land to confirm the app can
   read it.
8. **Review and finish.** You land on the sign-in page. Sign in with the Administrator account you just
   created.

## 5. Use the tray app

DBTracker Tray starts with Windows and sits next to the clock. Its icon is green when DBTracker is
healthy, amber when it's degraded or setup is pending, red when the site isn't responding.

To see backup counts in the tray, sign in to DBTracker, open **Access tokens** from the user menu,
create a token, then in the tray choose **Connect to DBTracker** and paste it.

## 6. Next steps

- Add instances (Instances, then Add instance, or import a CSV), and set each one's backup file
  location so scans can find backup files.
- Add users under **Users** and give each the right role.
- For HTTPS, add an HTTPS binding for the site in IIS Manager.

## Upgrade

Run the newer DBTracker-Setup.exe. It replaces the program files and keeps your configuration in
`App_Data`; database changes apply on the next start.

## Uninstall

Settings > Apps > **DBTracker Setup** > Uninstall. Your configuration in `App_Data`, the database, the
SQL login and the prerequisites stay. To remove `App_Data` too:
`DBTracker-Setup.exe /uninstall /quiet RemoveData=1`.

## Help

- Setup logs: `%TEMP%\DBTracker-Setup_*.log`
- Something not working: see Troubleshooting in [DEPLOYMENT.md](DEPLOYMENT.md), or open an issue on
  [GitHub](https://github.com/architonixlabs/DBTracker/issues).
