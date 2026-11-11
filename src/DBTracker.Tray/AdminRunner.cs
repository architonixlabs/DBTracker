using System;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;
using DBTracker.Tray.Core;

namespace DBTracker.Tray
{
    // Runs inside the elevated copy of the tray exe started with --admin <action> --result <file>.
    internal static class AdminRunner
    {
        public static int Run(string actionName, string resultFile)
        {
            string message;
            int exitCode;
            try
            {
                if (!AdminCommands.TryParse(actionName, out var action))
                {
                    message = "Unknown admin action \"" + actionName + "\".";
                    exitCode = 87;
                }
                else
                {
                    var settings = RegistrySettings.Load();
                    exitCode = action == AdminAction.CopySetupKey ? CopySetupKey(settings, out message) : RunAppCmd(action, settings, out message);
                }
            }
            catch (Exception ex)
            {
                message = ex.Message;
                exitCode = 1;
            }

            if (!string.IsNullOrEmpty(resultFile))
            {
                try { File.WriteAllText(resultFile, message, Encoding.UTF8); } catch (IOException) { }
            }
            return exitCode;
        }

        private static int RunAppCmd(AdminAction action, TraySettings settings, out string message)
        {
            var appcmd = Path.Combine(Environment.SystemDirectory, "inetsrv", "appcmd.exe");
            if (!File.Exists(appcmd))
            {
                message = "IIS management tools (appcmd.exe) were not found on this computer.";
                return 2;
            }

            var output = new StringBuilder();
            var arguments = AdminCommands.AppCmdArguments(action, settings);
            for (var i = 0; i < arguments.Count; i++)
            {
                var start = new ProcessStartInfo(appcmd, arguments[i])
                {
                    UseShellExecute = false,
                    CreateNoWindow = true,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                };
                using (var process = Process.Start(start))
                {
                    var text = (process.StandardOutput.ReadToEnd() + process.StandardError.ReadToEnd()).Trim();
                    process.WaitForExit();
                    output.AppendLine(text);
                    // Stopping an already-stopped pool before a restart isn't a failure.
                    var ignorable = action == AdminAction.RestartAppPool && i == 0;
                    if (process.ExitCode != 0 && !ignorable)
                    {
                        message = AdminCommands.Describe(action) + " failed: " + text;
                        return process.ExitCode;
                    }
                }
            }

            message = AdminCommands.Describe(action) + ": done.";
            return 0;
        }

        private static int CopySetupKey(TraySettings settings, out string message)
        {
            var keyFile = Path.Combine(settings.AppDataFolder(RegistrySettings.DataDirOverride()), "setup-key.txt");
            if (!File.Exists(keyFile))
            {
                message = "There is no setup key. Setup is already finished, or DBTracker hasn't started yet.";
                return 3;
            }
            Clipboard.SetText(File.ReadAllText(keyFile).Trim());
            message = "Setup key copied to the clipboard.";
            return 0;
        }
    }

    // Starts the elevated copy (UAC prompt) from the normal tray process and waits for its answer.
    internal static class Elevation
    {
        public static Task<string> RunAsync(AdminAction action)
        {
            return Task.Run(() =>
            {
                var resultFile = Path.Combine(Path.GetTempPath(), "DBTrackerTray-" + Guid.NewGuid().ToString("N") + ".txt");
                var start = new ProcessStartInfo(Application.ExecutablePath,
                    AdminCommands.Switch + " " + AdminCommands.ToArgument(action) + " --result \"" + resultFile + "\"")
                {
                    UseShellExecute = true,
                    Verb = "runas",
                };
                try
                {
                    using (var process = Process.Start(start))
                    {
                        process.WaitForExit();
                    }
                    return File.Exists(resultFile) ? File.ReadAllText(resultFile) : AdminCommands.Describe(action) + ": no result was reported.";
                }
                catch (Win32Exception ex) when (ex.NativeErrorCode == 1223)
                {
                    return AdminCommands.Describe(action) + " was cancelled.";
                }
                finally
                {
                    try { File.Delete(resultFile); } catch (IOException) { }
                }
            });
        }
    }
}
