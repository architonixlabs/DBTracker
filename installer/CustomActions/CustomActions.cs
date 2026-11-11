using System;
using System.Data.SqlClient;
using System.Diagnostics;
using System.IO;
using System.Net;
using System.Runtime.InteropServices;
using System.Security.AccessControl;
using System.Security.Principal;
using System.Threading;
using WixToolset.Dtf.WindowsInstaller;

namespace DBTracker.CustomActions
{
    public static class CustomActions
    {
        private const string ApplicationHostConfig = @"inetsrv\config\applicationHost.config";

        [CustomAction]
        public static ActionResult DetectPrerequisites(Session session)
        {
            var programFiles = InstallerChecks.ProgramFiles64();
            var aspNetCore = InstallerChecks.HasAspNetCore10(programFiles);
            var ancm = InstallerChecks.HasAspNetCoreModuleV2(programFiles);
            session["ASPNETCORE10_FOUND"] = aspNetCore ? "1" : "";
            session["ANCMV2_FOUND"] = ancm ? "1" : "";
            session.Log("DBTracker: ASP.NET Core 10 runtime " + (aspNetCore ? "found" : "not found") + ", ASP.NET Core Module V2 " + (ancm ? "found" : "not found") + " under " + programFiles);
            return ActionResult.Success;
        }

        [CustomAction]
        public static ActionResult ValidateSettingsUi(Session session)
        {
            session["DBT_SETTINGS_ERROR"] = Validate(session) ?? "";
            return ActionResult.Success;
        }

        [CustomAction]
        public static ActionResult ValidateSettingsExecute(Session session)
        {
            var error = Validate(session);
            if (error == null)
            {
                return ActionResult.Success;
            }
            session.Log("DBTracker: settings rejected: " + error);
            using (var record = new Record(0))
            {
                record.FormatString = error;
                session.Message(InstallMessage.Error | (InstallMessage)MessageButtons.OK | (InstallMessage)MessageIcon.Error, record);
            }
            return ActionResult.Failure;
        }

        private static string Validate(Session session)
        {
            var error = InstallerChecks.ValidateName("Site name", session["SITE_NAME"])
                ?? InstallerChecks.ValidateName("App pool name", session["APPPOOL_NAME"]);
            if (error != null)
            {
                return error;
            }
            error = InstallerChecks.ValidatePort(session["SITE_PORT"], out var port);
            if (error != null)
            {
                return error;
            }
            if (session["SQL_GRANT_LOGIN"] == "1")
            {
                error = InstallerChecks.ValidateSqlServer(session["SQL_SERVER"]);
                if (error != null)
                {
                    return error;
                }
            }
            var config = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.System), ApplicationHostConfig);
            var isUpgradeOrRepair = !string.IsNullOrEmpty(session["Installed"]) || !string.IsNullOrEmpty(session["WIX_UPGRADE_DETECTED"]);
            return InstallerChecks.FindPortConflict(port, session["SITE_NAME"], config, isUpgradeOrRepair);
        }

        // Deferred, runs as LocalSystem after the IIS extension has written the pool and site.
        [CustomAction]
        public static ActionResult ConfigureAppPoolAndData(Session session)
        {
            var pool = session.CustomActionData["Pool"];
            var appData = session.CustomActionData["AppData"];

            // The IIS extension writes nothing when ManagedRuntimeVersion is empty, which leaves IIS's
            // default v4.0. ASP.NET Core runs in-process through the ASP.NET Core Module, so clear it.
            var appcmd = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.System), @"inetsrv\appcmd.exe");
            var exit = Run(session, appcmd, "set apppool \"/apppool.name:" + pool + "\" /managedRuntimeVersion:\"\"");
            if (exit != 0)
            {
                return Fail(session, "Could not set the IIS app pool \"" + pool + "\" to No Managed Code (appcmd exit code " + exit + "). See the install log for details.");
            }

            // StopAppPool may have stopped an existing pool before files were replaced; start it again.
            // A non-zero exit here usually means it is already running, so it is logged, not fatal.
            Run(session, appcmd, "start apppool \"/apppool.name:" + pool + "\"");

            try
            {
                Directory.CreateDirectory(appData);
                var identity = ResolveAppPoolIdentity(session, pool);
                var security = Directory.GetAccessControl(appData);
                security.AddAccessRule(new FileSystemAccessRule(
                    identity,
                    FileSystemRights.Modify | FileSystemRights.Synchronize,
                    InheritanceFlags.ContainerInherit | InheritanceFlags.ObjectInherit,
                    PropagationFlags.None,
                    AccessControlType.Allow));
                Directory.SetAccessControl(appData, security);
                session.Log("DBTracker: granted Modify on " + appData + " to IIS APPPOOL\\" + pool + " (" + identity.Value + ")");
            }
            catch (Exception ex)
            {
                return Fail(session, "Could not give the app pool identity IIS APPPOOL\\" + pool + " write access to " + appData + ": " + ex.Message);
            }
            return ActionResult.Success;
        }

        // Deferred, before files change: in-process hosting loads the app's DLLs into w3wp.exe, so a
        // running pool would lock them during an upgrade, repair or uninstall.
        [CustomAction]
        public static ActionResult StopAppPool(Session session)
        {
            var pool = session.CustomActionData["Pool"];
            var appcmd = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.System), @"inetsrv\appcmd.exe");
            if (File.Exists(appcmd))
            {
                Run(session, appcmd, "stop apppool \"/apppool.name:" + pool + "\"");
                Thread.Sleep(2000);
            }
            return ActionResult.Success;
        }

        private static SecurityIdentifier ResolveAppPoolIdentity(Session session, string pool)
        {
            for (var attempt = 1; attempt <= 5; attempt++)
            {
                try
                {
                    return (SecurityIdentifier)new NTAccount("IIS APPPOOL", pool).Translate(typeof(SecurityIdentifier));
                }
                catch (IdentityNotMappedException)
                {
                    session.Log("DBTracker: IIS APPPOOL\\" + pool + " not resolvable yet (attempt " + attempt + ")");
                    Thread.Sleep(1000);
                }
            }
            var sid = InstallerChecks.AppPoolSid(pool);
            session.Log("DBTracker: using the derived app pool SID " + sid.Value);
            return sid;
        }

        // Deferred and impersonated: the installing user's Windows credentials are what SQL Server sees.
        // A failure is shown and logged but never rolls back the install; the login can be created later.
        [CustomAction]
        public static ActionResult GrantSqlLogin(Session session)
        {
            var server = session.CustomActionData["Server"];
            var pool = session.CustomActionData["Pool"];
            string login;
            if (InstallerChecks.IsLocalSqlServer(server))
            {
                login = "IIS APPPOOL\\" + pool;
            }
            else
            {
                var domain = GetJoinedDomain();
                if (domain == null)
                {
                    Warn(session, "SQL Server " + server + " is on another computer, but this computer isn't in a domain, so the app pool can't sign in to it with Windows authentication. DBTracker is installed. In the setup wizard, use a SQL login instead.");
                    return ActionResult.Success;
                }
                login = domain + "\\" + Environment.MachineName + "$";
            }

            var builder = new SqlConnectionStringBuilder
            {
                DataSource = server,
                InitialCatalog = "master",
                IntegratedSecurity = true,
                ConnectTimeout = 15,
                TrustServerCertificate = true,
            };
            const string sql =
                "IF SUSER_ID(@login) IS NULL EXEC(N'CREATE LOGIN ' + QUOTENAME(@login) + N' FROM WINDOWS'); " +
                "EXEC(N'ALTER SERVER ROLE dbcreator ADD MEMBER ' + QUOTENAME(@login));";
            try
            {
                using (var connection = new SqlConnection(builder.ConnectionString))
                using (var command = new SqlCommand(sql, connection))
                {
                    command.Parameters.Add("@login", System.Data.SqlDbType.NVarChar, 128).Value = login;
                    connection.Open();
                    command.ExecuteNonQuery();
                }
                session.Log("DBTracker: SQL login " + login + " exists on " + server + " and is in dbcreator");
            }
            catch (Exception ex)
            {
                Warn(session, "Could not create the SQL login " + login + " on " + server + ": " + ex.Message + " DBTracker is installed. Ask a SQL Server administrator to run the statements in DEPLOYMENT.md section 5.");
            }
            return ActionResult.Success;
        }

        [CustomAction]
        public static ActionResult RemoveAppData(Session session)
        {
            var appData = session.CustomActionData["AppData"];
            // The worker process can take a moment to exit after the pool is removed.
            for (var attempt = 1; attempt <= 5 && Directory.Exists(appData); attempt++)
            {
                try
                {
                    Directory.Delete(appData, true);
                    session.Log("DBTracker: deleted " + appData + " (REMOVE_DATA=1)");
                }
                catch (Exception ex) when (ex is IOException || ex is UnauthorizedAccessException)
                {
                    session.Log("DBTracker: could not delete " + appData + " yet (attempt " + attempt + "): " + ex.Message);
                    Thread.Sleep(2000);
                }
            }
            return ActionResult.Success;
        }

        // Immediate, after the install (or from the exit dialog). Never fails the install.
        [CustomAction]
        public static ActionResult LaunchWizard(Session session)
        {
            try
            {
                var port = session["SITE_PORT"];
                var dataDir = Environment.GetEnvironmentVariable("DBTRACKER_DATA_DIR", EnvironmentVariableTarget.Machine);
                if (string.IsNullOrWhiteSpace(dataDir))
                {
                    dataDir = Path.Combine(session["INSTALLFOLDER"], "App_Data");
                }
                var configured = File.Exists(Path.Combine(dataDir, "dbtracker.config.json"));
                var url = "http://localhost:" + port + (configured ? "/" : "/install");

                // The first request starts the app, which writes the setup key on first start.
                WarmUp(session, url, TimeSpan.FromSeconds(30));
                var keyFile = Path.Combine(dataDir, "setup-key.txt");
                if (!configured)
                {
                    WaitForFile(keyFile, TimeSpan.FromSeconds(10));
                }

                OpenUnelevated(session, url);
                if (!configured && session["OPEN_SETUP_KEY"] == "1" && File.Exists(keyFile))
                {
                    OpenUnelevated(session, keyFile);
                }
                session.Log("DBTracker: opened " + url + (File.Exists(keyFile) ? "; setup key at " + keyFile : ""));
            }
            catch (Exception ex)
            {
                session.Log("DBTracker: could not open the setup wizard: " + ex.Message);
            }
            return ActionResult.Success;
        }

        private static void WarmUp(Session session, string url, TimeSpan timeout)
        {
            var deadline = DateTime.UtcNow + timeout;
            while (DateTime.UtcNow < deadline)
            {
                try
                {
                    var request = (HttpWebRequest)WebRequest.Create(url);
                    request.Timeout = 10000;
                    request.AllowAutoRedirect = false;
                    using (request.GetResponse())
                    {
                        return;
                    }
                }
                catch (WebException ex) when (ex.Response != null)
                {
                    ex.Response.Dispose();
                    return;
                }
                catch (WebException ex)
                {
                    session.Log("DBTracker: waiting for the site to answer: " + ex.Message);
                    Thread.Sleep(2000);
                }
            }
        }

        private static void WaitForFile(string path, TimeSpan timeout)
        {
            var deadline = DateTime.UtcNow + timeout;
            while (!File.Exists(path) && DateTime.UtcNow < deadline)
            {
                Thread.Sleep(500);
            }
        }

        // The install may be elevated. Handing the target to explorer.exe opens it in the signed-in
        // user's existing (unelevated) shell instead of starting an elevated browser or editor.
        private static void OpenUnelevated(Session session, string target)
        {
            try
            {
                Process.Start(new ProcessStartInfo("explorer.exe", "\"" + target + "\"") { UseShellExecute = false });
            }
            catch (Exception ex)
            {
                session.Log("DBTracker: explorer.exe could not open " + target + " (" + ex.Message + "), trying the shell directly");
                Process.Start(new ProcessStartInfo(target) { UseShellExecute = true });
            }
        }

        private static int Run(Session session, string fileName, string arguments)
        {
            var start = new ProcessStartInfo(fileName, arguments)
            {
                UseShellExecute = false,
                CreateNoWindow = true,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
            };
            using (var process = Process.Start(start))
            {
                var output = process.StandardOutput.ReadToEnd() + process.StandardError.ReadToEnd();
                process.WaitForExit();
                session.Log("DBTracker: " + Path.GetFileName(fileName) + " " + arguments + " -> " + process.ExitCode + " " + output.Trim());
                return process.ExitCode;
            }
        }

        private static ActionResult Fail(Session session, string message)
        {
            session.Log("DBTracker: " + message);
            using (var record = new Record(0))
            {
                record.FormatString = message;
                session.Message(InstallMessage.Error | (InstallMessage)MessageButtons.OK | (InstallMessage)MessageIcon.Error, record);
            }
            return ActionResult.Failure;
        }

        private static void Warn(Session session, string message)
        {
            session.Log("DBTracker: warning: " + message);
            using (var record = new Record(0))
            {
                record.FormatString = message;
                session.Message(InstallMessage.Warning | (InstallMessage)MessageButtons.OK | (InstallMessage)MessageIcon.Warning, record);
            }
        }

        private static string GetJoinedDomain()
        {
            IntPtr buffer = IntPtr.Zero;
            try
            {
                if (NetGetJoinInformation(null, out buffer, out var status) != 0 || status != NetJoinStatus.DomainName)
                {
                    return null;
                }
                return Marshal.PtrToStringUni(buffer);
            }
            finally
            {
                if (buffer != IntPtr.Zero)
                {
                    NetApiBufferFree(buffer);
                }
            }
        }

        private enum NetJoinStatus
        {
            Unknown = 0,
            Unjoined = 1,
            WorkgroupName = 2,
            DomainName = 3,
        }

        [DllImport("netapi32.dll", CharSet = CharSet.Unicode)]
        private static extern int NetGetJoinInformation(string server, out IntPtr nameBuffer, out NetJoinStatus status);

        [DllImport("netapi32.dll")]
        private static extern int NetApiBufferFree(IntPtr buffer);
    }
}
