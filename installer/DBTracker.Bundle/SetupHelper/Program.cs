using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.ServiceProcess;

namespace DBTracker.SetupHelper
{
    // Burn can only run packages it carries, so system tools like dism.exe and net.exe are run from here.
    internal static class Program
    {
        private const int Success = 0;
        private const int Failure = 1;
        private const int InvalidArguments = 87;
        private const int RebootRequired = 3010;

        // DISM optional-feature names; the same names work on Windows client and Windows Server.
        // /All also enables each feature's parents.
        internal static readonly string[] IisFeatures =
        {
            "IIS-WebServerRole",
            "IIS-WebServer",
            "IIS-CommonHttpFeatures",
            "IIS-StaticContent",
            "IIS-DefaultDocument",
            "IIS-HttpErrors",
            "IIS-HealthAndDiagnostics",
            "IIS-HttpLogging",
            "IIS-Security",
            "IIS-RequestFiltering",
            "IIS-Performance",
            "IIS-HttpCompressionStatic",
            "IIS-ApplicationDevelopment",
            "IIS-WebSockets",
            "IIS-WebServerManagementTools",
            "IIS-ManagementConsole",
        };

        private static readonly string LogPath = Path.Combine(Path.GetTempPath(), "DBTracker-SetupHelper.log");

        private static int Main(string[] args)
        {
            var command = args.FirstOrDefault()?.Trim().ToLowerInvariant();
            try
            {
                switch (command)
                {
                    case "enable-iis":
                        return EnableIis();
                    case "restart-iis":
                        return RestartIis();
                    default:
                        Log("Usage: DBTracker.SetupHelper.exe enable-iis | restart-iis");
                        return InvalidArguments;
                }
            }
            catch (Exception ex)
            {
                Log($"{command} failed: {ex}");
                return Failure;
            }
        }

        private static int EnableIis()
        {
            var dism = Path.Combine(Environment.SystemDirectory, "dism.exe");
            var featureArgs = string.Join(" ", IisFeatures.Select(f => "/FeatureName:" + f));
            var exitCode = Run(dism, $"/Online /Enable-Feature {featureArgs} /All /NoRestart /Quiet");

            if (exitCode == Success || exitCode == RebootRequired)
            {
                Log(exitCode == RebootRequired
                    ? "IIS features enabled. Windows reports a restart is needed to finish."
                    : "IIS features enabled.");
                return exitCode;
            }

            Log($"DISM could not enable the IIS features (exit code {exitCode}). See %windir%\\Logs\\DISM\\dism.log.");
            return exitCode;
        }

        // The Hosting Bundle changes the machine PATH and registers the ASP.NET Core Module; IIS only picks
        // both up after WAS and W3SVC restart (Microsoft's documented "net stop was /y" + "net start w3svc").
        private static int RestartIis()
        {
            var net = Path.Combine(Environment.SystemDirectory, "net.exe");
            Run(net, "stop was /y");
            Run(net, "start w3svc");

            using (var w3svc = new ServiceController("W3SVC"))
            {
                w3svc.WaitForStatus(ServiceControllerStatus.Running, TimeSpan.FromSeconds(60));
                Log("IIS restarted (W3SVC running).");
            }

            return Success;
        }

        private static int Run(string fileName, string arguments)
        {
            Log($"> {Path.GetFileName(fileName)} {arguments}");
            var psi = new ProcessStartInfo(fileName, arguments)
            {
                UseShellExecute = false,
                CreateNoWindow = true,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
            };

            using (var process = Process.Start(psi))
            {
                var stdout = process.StandardOutput.ReadToEndAsync();
                var stderr = process.StandardError.ReadToEndAsync();
                process.WaitForExit();
                var output = (stdout.Result + stderr.Result).Trim();
                if (output.Length > 0)
                {
                    Log(output);
                }

                Log($"exit code {process.ExitCode}");
                return process.ExitCode;
            }
        }

        private static void Log(string message)
        {
            var line = $"{DateTime.Now:yyyy-MM-dd HH:mm:ss} {message}";
            Console.WriteLine(line);
            try
            {
                File.AppendAllText(LogPath, line + Environment.NewLine);
            }
            catch (IOException)
            {
            }
            catch (UnauthorizedAccessException)
            {
            }
        }
    }
}
