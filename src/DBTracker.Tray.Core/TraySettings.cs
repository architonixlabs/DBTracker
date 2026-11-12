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
        private static readonly Regex HostName = new Regex(
            @"^(?=.{1,253}$)[A-Za-z0-9]([A-Za-z0-9-]{0,61}[A-Za-z0-9])?(\.[A-Za-z0-9]([A-Za-z0-9-]{0,61}[A-Za-z0-9])?)*$",
            RegexOptions.CultureInvariant);

        /// <summary>The site's HTTP port.</summary>
        public int Port { get; private set; } = DefaultPort;
        /// <summary>"https" when the MSI recorded HTTPS with a valid port, else "http".</summary>
        public string Scheme { get; private set; } = "http";
        public int HttpsPort { get; private set; }
        /// <summary>The name on the site's certificate, for browser links; "localhost" when none was recorded.</summary>
        public string BrowserHost { get; private set; } = "localhost";
        public string InstallFolder { get; private set; } = DefaultInstallFolder;
        public string SiteName { get; private set; } = DefaultName;
        public string AppPoolName { get; private set; } = DefaultName;

        public bool UsesHttps => Scheme == "https";
        /// <summary>The port the tray polls: the HTTPS port when HTTPS is on.</summary>
        public int ActivePort => UsesHttps ? HttpsPort : Port;

        public static TraySettings From(string port, string installFolder, string siteName, string appPoolName) =>
            From(port, installFolder, siteName, appPoolName, null, null, null);

        /// <summary>HTTPS is used only when the scheme says so and the HTTPS port is valid; anything else stays on http.</summary>
        public static TraySettings From(string port, string installFolder, string siteName, string appPoolName,
            string scheme, string httpsPort, string certHost)
        {
            var settings = new TraySettings();
            if (int.TryParse((port ?? "").Trim(), out var p) && p > 0 && p <= 65535)
            {
                settings.Port = p;
            }
            if (string.Equals((scheme ?? "").Trim(), "https", StringComparison.OrdinalIgnoreCase)
                && int.TryParse((httpsPort ?? "").Trim(), out var hp) && hp > 0 && hp <= 65535)
            {
                settings.Scheme = "https";
                settings.HttpsPort = hp;
                var host = (certHost ?? "").Trim().TrimEnd('.');
                if (HostName.IsMatch(host))
                {
                    settings.BrowserHost = host.ToLowerInvariant();
                }
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

        /// <summary>What the tray polls: always localhost, so its requests and token never leave the machine.</summary>
        public string BaseUrl => UsesHttps ? "https://localhost:" + HttpsPort : "http://localhost:" + Port;
        public string HealthUrl => BaseUrl + "/healthz";
        public string SummaryUrl => BaseUrl + "/api/tray/summary";

        /// <summary>What the browser opens: the certificate's name, so the browser doesn't warn about a name mismatch.</summary>
        public string BrowserBaseUrl => UsesHttps
            ? "https://" + BrowserHost + (HttpsPort == 443 ? "" : ":" + HttpsPort)
            : BaseUrl;
        public string TokensUrl => BrowserBaseUrl + "/account/tokens";

        public string OpenUrl(bool setupRequired) => BrowserBaseUrl + (setupRequired ? "/install" : "/");

        public string DashboardUrl(TraySummary summary)
        {
            var path = summary?.DashboardUrl;
            if (string.IsNullOrWhiteSpace(path) || !path.StartsWith("/", StringComparison.Ordinal) || path.StartsWith("//", StringComparison.Ordinal))
            {
                path = "/";
            }
            return BrowserBaseUrl + path;
        }

        public string AppDataFolder(string dataDirOverride) =>
            string.IsNullOrWhiteSpace(dataDirOverride) ? Path.Combine(InstallFolder, "App_Data") : dataDirOverride.Trim();

        public string DeploymentGuide => Path.Combine(InstallFolder, "DEPLOYMENT.md");
    }
}
