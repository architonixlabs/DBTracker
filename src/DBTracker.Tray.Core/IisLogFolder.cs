using System;
using System.IO;
using System.Linq;
using System.Xml.Linq;

namespace DBTracker.Tray.Core
{
    // IIS writes each site's logs to <logFile directory>\W3SVC<site id>. The directory comes from the
    // site's own logFile element, else siteDefaults, else the IIS default.
    public static class IisLogFolder
    {
        public const string IisDefault = @"%SystemDrive%\inetpub\logs\LogFiles";

        // Returns the unexpanded folder, or null when the site isn't in the config.
        public static string Find(string applicationHostXml, string siteName)
        {
            if (string.IsNullOrWhiteSpace(applicationHostXml) || string.IsNullOrWhiteSpace(siteName))
            {
                return null;
            }

            XDocument doc;
            try
            {
                doc = XDocument.Parse(applicationHostXml);
            }
            catch (Exception ex) when (ex is System.Xml.XmlException)
            {
                return null;
            }

            var sites = doc.Descendants("sites").FirstOrDefault();
            var site = sites?.Elements("site").FirstOrDefault(s => string.Equals((string)s.Attribute("name"), siteName, StringComparison.OrdinalIgnoreCase));
            var id = (string)site?.Attribute("id");
            if (string.IsNullOrEmpty(id))
            {
                return null;
            }

            var directory = (string)site.Element("logFile")?.Attribute("directory")
                ?? (string)sites.Element("siteDefaults")?.Element("logFile")?.Attribute("directory")
                ?? IisDefault;
            return Path.Combine(directory, "W3SVC" + id);
        }
    }
}
