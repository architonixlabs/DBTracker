using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.NetworkInformation;
using System.Security.Cryptography;
using System.Security.Principal;
using System.Text;
using System.Text.RegularExpressions;
using System.Xml.Linq;

namespace DBTracker.CustomActions
{
    public sealed class SiteBinding
    {
        public string SiteName { get; set; }
        public string Protocol { get; set; }
        public int Port { get; set; }
    }

    public static class InstallerChecks
    {
        // Pool and site names end up inside appcmd arguments, icacls-style account names and a
        // QUOTENAME'd SQL login, so keep them to characters that need no escaping anywhere.
        private static readonly Regex SafeName = new Regex(@"^[A-Za-z0-9][A-Za-z0-9 ._-]{0,63}$", RegexOptions.CultureInvariant);

        public static string ValidateName(string label, string value)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                return label + " is required.";
            }
            if (!SafeName.IsMatch(value) || value.EndsWith(" ", StringComparison.Ordinal) || value.EndsWith(".", StringComparison.Ordinal))
            {
                return label + " \"" + value + "\" can only use letters, digits, spaces, dots, hyphens and underscores (up to 64 characters), and must start with a letter or digit.";
            }
            return null;
        }

        public static string ValidatePort(string value, out int port)
        {
            port = 0;
            if (!int.TryParse(value, out port) || port < 1 || port > 65535)
            {
                return "Port \"" + value + "\" must be a whole number from 1 to 65535.";
            }
            return null;
        }

        public static string ValidateSqlServer(string value)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                return "Enter the SQL Server name, or clear the option to give the app pool access to SQL Server.";
            }
            if (value.IndexOfAny(new[] { ';', '=', '"', '\'' }) >= 0)
            {
                return "SQL Server name \"" + value + "\" contains a character that isn't allowed in a server name.";
            }
            return null;
        }

        public static List<SiteBinding> ReadIisBindings(string applicationHostConfigPath)
        {
            var doc = XDocument.Load(applicationHostConfigPath);
            var result = new List<SiteBinding>();
            foreach (var site in doc.Descendants("sites").Elements("site"))
            {
                var name = (string)site.Attribute("name") ?? "";
                foreach (var binding in site.Descendants("binding"))
                {
                    var protocol = (string)binding.Attribute("protocol") ?? "";
                    if (!protocol.Equals("http", StringComparison.OrdinalIgnoreCase) && !protocol.Equals("https", StringComparison.OrdinalIgnoreCase))
                    {
                        continue;
                    }
                    if (TryParseBindingPort((string)binding.Attribute("bindingInformation"), out var port))
                    {
                        result.Add(new SiteBinding { SiteName = name, Protocol = protocol, Port = port });
                    }
                }
            }
            return result;
        }

        // bindingInformation is "IP:port:host"; the IP part may itself be a bracketed IPv6 address.
        public static bool TryParseBindingPort(string bindingInformation, out int port)
        {
            port = 0;
            if (string.IsNullOrEmpty(bindingInformation))
            {
                return false;
            }
            var lastColon = bindingInformation.LastIndexOf(':');
            if (lastColon <= 0)
            {
                return false;
            }
            var beforeHost = bindingInformation.Substring(0, lastColon);
            var portColon = beforeHost.LastIndexOf(':');
            var portText = portColon >= 0 ? beforeHost.Substring(portColon + 1) : beforeHost;
            return int.TryParse(portText, out port);
        }

        public static string FindPortConflict(int port, string ownSiteName, string applicationHostConfigPath, bool isUpgradeOrRepair)
        {
            try
            {
                var clash = ReadIisBindings(applicationHostConfigPath)
                    .FirstOrDefault(b => b.Port == port && !b.SiteName.Equals(ownSiteName, StringComparison.OrdinalIgnoreCase));
                return clash == null
                    ? null
                    : "Port " + port + " is already used by the IIS site \"" + clash.SiteName + "\". Choose another port, for example in the 8xxx or 9xxx range.";
            }
            catch (Exception ex) when (ex is UnauthorizedAccessException || ex is IOException || ex is System.Xml.XmlException)
            {
                // Without admin rights applicationHost.config is unreadable; fall back to what is listening now.
                // An upgrade of our own site would be listening on this port, so only flag it on a first install.
                if (isUpgradeOrRepair)
                {
                    return null;
                }
                var listening = IPGlobalProperties.GetIPGlobalProperties().GetActiveTcpListeners().Any(e => e.Port == port);
                return listening
                    ? "Port " + port + " is already in use on this computer. Choose another port, for example in the 8xxx or 9xxx range."
                    : null;
            }
        }

        public static bool IsLocalSqlServer(string server)
        {
            if (string.IsNullOrWhiteSpace(server))
            {
                return false;
            }
            var host = server.Trim();
            if (host.StartsWith("tcp:", StringComparison.OrdinalIgnoreCase))
            {
                host = host.Substring(4);
            }
            var cut = host.IndexOfAny(new[] { '\\', ',' });
            if (cut >= 0)
            {
                host = host.Substring(0, cut);
            }
            host = host.Trim();
            var locals = new[] { ".", "(local)", "localhost", "127.0.0.1", "::1", Environment.MachineName };
            return locals.Any(l => l.Equals(host, StringComparison.OrdinalIgnoreCase));
        }

        // IIS app pool virtual accounts are S-1-5-82 followed by the SHA-1 of the lower-cased pool name
        // (UTF-16LE) as five 32-bit sub-authorities (checked against Windows' own lookup of
        // IIS APPPOOL\DefaultAppPool). Used when the account name can't be resolved yet, for example
        // right after the pool is created.
        public static SecurityIdentifier AppPoolSid(string poolName)
        {
            byte[] hash;
            using (var sha1 = SHA1.Create())
            {
                hash = sha1.ComputeHash(Encoding.Unicode.GetBytes(poolName.ToLowerInvariant()));
            }
            var parts = new StringBuilder("S-1-5-82");
            for (var i = 0; i < 5; i++)
            {
                parts.Append('-').Append(BitConverter.ToUInt32(hash, i * 4));
            }
            return new SecurityIdentifier(parts.ToString());
        }

        public static bool HasAspNetCore10(string programFiles)
        {
            var dir = Path.Combine(programFiles, "dotnet", "shared", "Microsoft.AspNetCore.App");
            return Directory.Exists(dir)
                && Directory.GetDirectories(dir).Any(d => Path.GetFileName(d).StartsWith("10.", StringComparison.Ordinal));
        }

        // The .NET 10 Hosting Bundle ships ASP.NET Core Module V2 file versions 20.x (the bundle checks
        // for at least 20.0.26302.15, the build it installs). Older bundles leave an older module that
        // can't host a .NET 10 app, so require the major version, not just the file.
        public const int MinimumModuleMajorVersion = 20;

        public static bool HasAspNetCoreModuleV2(string programFiles)
        {
            var path = Path.Combine(programFiles, "IIS", "Asp.Net Core Module", "V2", "aspnetcorev2.dll");
            return File.Exists(path)
                && System.Diagnostics.FileVersionInfo.GetVersionInfo(path).FileMajorPart >= MinimumModuleMajorVersion;
        }

        public static string ProgramFiles64()
        {
            return Environment.GetEnvironmentVariable("ProgramW6432")
                ?? Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles);
        }
    }
}
