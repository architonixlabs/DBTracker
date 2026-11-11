using System;
using System.IO;
using System.Text.RegularExpressions;

namespace DBTracker.Tray.Core
{
    // Where DBTracker lives, read from what the MSI records under HKLM\SOFTWARE\Architonix Labs\DBTracker.
    public sealed class TraySettings
    {
        public const int DefaultPort = 8150;
        public const string DefaultInstallFolder = @"C:\inetpub\DBTracker";
        public const string DefaultName = "DBTracker";
        public const string RepositoryUrl = "https://github.com/architonixlabs/DBTracker";

        private static readonly Regex SafeName = new Regex(@"^[A-Za-z0-9 ._-]{1,64}$", RegexOptions.CultureInvariant);

        public int Port { get; private set; } = DefaultPort;
        public string InstallFolder { get; private set; } = DefaultInstallFolder;
        public string SiteName { get; private set; } = DefaultName;
        public string AppPoolName { get; private set; } = DefaultName;

        public static TraySettings From(string port, string installFolder, string siteName, string appPoolName)
        {
            var settings = new TraySettings();
            if (int.TryParse((port ?? "").Trim(), out var p) && p > 0 && p <= 65535)
            {
                settings.Port = p;
            }
            if (!string.IsNullOrWhiteSpace(installFolder))
            {
                settings.InstallFolder = installFolder.Trim().TrimEnd('\\');
            }
            if (IsSafeName(siteName))
            {
                settings.SiteName = siteName.Trim();
            }
            if (IsSafeName(appPoolName))
            {
                settings.AppPoolName = appPoolName.Trim();
            }
            return settings;
        }

        // Site and pool names reach appcmd.exe on the command line, so only plain names are accepted.
        public static bool IsSafeName(string name) => name != null && SafeName.IsMatch(name.Trim());

        public string BaseUrl => "http://localhost:" + Port;
        public string HealthUrl => BaseUrl + "/healthz";
        public string SummaryUrl => BaseUrl + "/api/tray/summary";
        public string TokensUrl => BaseUrl + "/account/tokens";

        public string OpenUrl(bool setupRequired) => BaseUrl + (setupRequired ? "/install" : "/");

        public string DashboardUrl(TraySummary summary)
        {
            var path = summary?.DashboardUrl;
            if (string.IsNullOrWhiteSpace(path) || !path.StartsWith("/", StringComparison.Ordinal) || path.StartsWith("//", StringComparison.Ordinal))
            {
                path = "/";
            }
            return BaseUrl + path;
        }

        public string AppDataFolder(string dataDirOverride) =>
            string.IsNullOrWhiteSpace(dataDirOverride) ? Path.Combine(InstallFolder, "App_Data") : dataDirOverride.Trim();

        public string DeploymentGuide => Path.Combine(InstallFolder, "DEPLOYMENT.md");
    }
}
