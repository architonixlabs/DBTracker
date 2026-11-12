using System;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;

namespace DBTracker.CustomActions
{
    /// <summary>What the installer needs to know about a certificate in LocalMachine\My.</summary>
    public sealed class CertificateInfo
    {
        public string Thumbprint { get; set; }
        public bool HasPrivateKey { get; set; }
        public DateTime NotBefore { get; set; }
        public DateTime NotAfter { get; set; }
        /// <summary>False when the certificate has an Enhanced Key Usage list without Server Authentication.</summary>
        public bool AllowsServerAuthentication { get; set; } = true;
        /// <summary>The DNS name the certificate is issued to, or null.</summary>
        public string DnsName { get; set; }
    }

    /// <summary>The HTTPS settings one install run ends up with.</summary>
    public sealed class HttpsChoice
    {
        /// <summary>Normalised thumbprint, or "" when the site stays HTTP only.</summary>
        public string Thumbprint { get; set; } = "";
        public int Port { get; set; }
        /// <summary>Host name the browser should use: the certificate's DNS name, or "localhost".</summary>
        public string BrowserHost { get; set; } = "localhost";
        public bool Enabled => Thumbprint.Length > 0;
        public string Scheme => Enabled ? "https" : "http";
    }

    public static class HttpsChecks
    {
        public const int DefaultHttpsPort = 443;
        /// <summary>Passing this as CERT_THUMBPRINT turns HTTPS off again on an upgrade or repair.</summary>
        public const string TurnOff = "none";
        /// <summary>The application id recorded with DBTracker's http.sys certificate binding.</summary>
        public const string SslCertAppId = "{B9B7166A-8502-4965-BA3E-0EA237AF389F}";

        private const string ServerAuthenticationOid = "1.3.6.1.5.5.7.3.1";
        private static readonly Regex Hex40 = new Regex("^[0-9A-F]{40}$", RegexOptions.CultureInvariant);
        private static readonly Regex HostName = new Regex(
            @"^(?=.{1,253}$)[A-Za-z0-9]([A-Za-z0-9-]{0,61}[A-Za-z0-9])?(\.[A-Za-z0-9]([A-Za-z0-9-]{0,61}[A-Za-z0-9])?)*$",
            RegexOptions.CultureInvariant);

        /// <summary>
        /// Upper-case hex with spaces, colons and the invisible marks that the Windows certificate dialog adds when a
        /// thumbprint is copied from it (U+200E and friends) removed. Null or blank gives "".
        /// </summary>
        public static string NormalizeThumbprint(string value)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                return "";
            }
            var builder = new StringBuilder(value.Length);
            foreach (var c in value)
            {
                if (char.IsWhiteSpace(c) || c == ':' || c == '‎' || c == '‏' || c == '​' || c == '﻿' || c == '‪' || c == '‬')
                {
                    continue;
                }
                builder.Append(char.ToUpperInvariant(c));
            }
            return builder.ToString();
        }

        public static bool IsTurnOff(string value) =>
            value != null && value.Trim().Equals(TurnOff, StringComparison.OrdinalIgnoreCase);

        /// <summary>Null when the normalised value is a SHA-1 thumbprint (40 hex digits).</summary>
        public static string ValidateThumbprintFormat(string normalized)
        {
            if (Hex40.IsMatch(normalized ?? ""))
            {
                return null;
            }
            return "Certificate thumbprint \"" + normalized + "\" is not valid. It must be the 40 hexadecimal characters shown as Thumbprint on the certificate's Details tab (Local Computer > Personal > Certificates).";
        }

        /// <summary>Null when the certificate can serve DBTracker's HTTPS binding.</summary>
        public static string CheckCertificate(string thumbprint, CertificateInfo certificate, DateTime utcNow, bool fromPreviousInstall)
        {
            var hint = fromPreviousInstall
                ? " This is the certificate the previous install used. Pass CERT_THUMBPRINT=<thumbprint> (Setup.exe: CertThumbprint=<thumbprint>) for its replacement, or CERT_THUMBPRINT=none to keep the site HTTP only."
                : "";
            if (certificate == null)
            {
                return "No certificate with thumbprint " + thumbprint + " is in the Local Computer > Personal certificate store (LocalMachine\\My). Import it there first, with its private key." + hint;
            }
            if (!certificate.HasPrivateKey)
            {
                return "The certificate " + thumbprint + " in LocalMachine\\My has no private key, so IIS can't use it for HTTPS. Import it again from a .pfx file that includes the private key." + hint;
            }
            if (certificate.NotAfter.ToUniversalTime() < utcNow)
            {
                return "The certificate " + thumbprint + " expired on " + certificate.NotAfter.ToString("yyyy-MM-dd") + ". Use a current certificate." + hint;
            }
            if (certificate.NotBefore.ToUniversalTime() > utcNow)
            {
                return "The certificate " + thumbprint + " is not valid until " + certificate.NotBefore.ToString("yyyy-MM-dd") + ". Use a certificate that is already valid." + hint;
            }
            if (!certificate.AllowsServerAuthentication)
            {
                return "The certificate " + thumbprint + " is not issued for Server Authentication, so browsers would reject it. Use a TLS server certificate." + hint;
            }
            return null;
        }

        /// <summary>
        /// The thumbprint for this run: the one given, else the one a previous install recorded. A given "none" turns
        /// HTTPS off. <paramref name="fromPreviousInstall"/> says the result came from the previous install.
        /// </summary>
        public static string ChooseThumbprint(string given, string previous, out bool fromPreviousInstall)
        {
            fromPreviousInstall = false;
            if (IsTurnOff(given))
            {
                return "";
            }
            var normalized = NormalizeThumbprint(given);
            if (normalized.Length > 0)
            {
                return normalized;
            }
            normalized = NormalizeThumbprint(previous);
            fromPreviousInstall = normalized.Length > 0;
            return normalized;
        }

        /// <summary>The HTTPS port: the one given, else the previous install's, else 443. It must differ from the HTTP port.</summary>
        public static string ValidateHttpsPort(string given, string previous, int httpPort, out int port)
        {
            var value = !string.IsNullOrWhiteSpace(given) ? given.Trim()
                : !string.IsNullOrWhiteSpace(previous) ? previous.Trim()
                : DefaultHttpsPort.ToString();
            port = 0;
            if (!int.TryParse(value, out port) || port < 1 || port > 65535)
            {
                return "HTTPS port \"" + value + "\" must be a whole number from 1 to 65535.";
            }
            if (port == httpPort)
            {
                return "The HTTPS port and the HTTP port are both " + port + ". Choose a different HTTPS port, for example 443 or one in the 8xxx or 9xxx range.";
            }
            return null;
        }

        /// <summary>The certificate's DNS name when it is a plain host name, else "localhost" (wildcards and odd names).</summary>
        public static string BrowserHost(string dnsName)
        {
            var name = (dnsName ?? "").Trim().TrimEnd('.');
            return HostName.IsMatch(name) ? name.ToLowerInvariant() : "localhost";
        }

        /// <summary>True when an Enhanced Key Usage list (OIDs) permits TLS server use. No list means any use.</summary>
        public static bool AllowsServerAuthentication(string[] enhancedKeyUsageOids) =>
            enhancedKeyUsageOids == null || enhancedKeyUsageOids.Length == 0 || enhancedKeyUsageOids.Contains(ServerAuthenticationOid);

        /// <summary>
        /// Where the browser should open DBTracker, from what the install recorded: https://&lt;certificate name&gt;[:port]
        /// when HTTPS is on, else http://localhost:&lt;port&gt;.
        /// </summary>
        public static string BrowserBaseUrl(string scheme, string httpsPort, string host, string httpPort)
        {
            if (string.Equals((scheme ?? "").Trim(), "https", StringComparison.OrdinalIgnoreCase)
                && int.TryParse((httpsPort ?? "").Trim(), out var port) && port > 0 && port <= 65535)
            {
                return "https://" + BrowserHost(host) + (port == DefaultHttpsPort ? "" : ":" + port);
            }
            return "http://localhost:" + (httpPort ?? "").Trim();
        }

        /// <summary>The netsh arguments that bind the certificate to the port in http.sys for every IP address.</summary>
        public static string NetshAddSslCertArguments(int port, string thumbprint) =>
            "http add sslcert ipport=0.0.0.0:" + port + " certhash=" + thumbprint + " appid=" + SslCertAppId + " certstorename=MY";

        public static string NetshDeleteSslCertArguments(int port) => "http delete sslcert ipport=0.0.0.0:" + port;

        public static string AppCmdBindingArguments(string siteName, int port, bool add) =>
            "set site \"/site.name:" + siteName + "\" /" + (add ? "+" : "-") + "bindings.[protocol='https',bindingInformation='*:" + port + ":']";
    }
}
