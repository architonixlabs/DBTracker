using System;

namespace DBTracker.Tray.Core
{
    // Picks the Application event log entries that belong to this DBTracker site.
    public static class EventLogFilter
    {
        private static readonly string[] Sources =
        {
            "IIS AspNetCore Module V2",
            "IIS Express AspNetCore Module V2",
            "ASP.NET Core Module",
            ".NET Runtime",
            "Application Error",
        };

        public static bool IsRelevantSource(string source)
        {
            foreach (var s in Sources)
            {
                if (string.Equals(s, source, StringComparison.OrdinalIgnoreCase))
                {
                    return true;
                }
            }
            return false;
        }

        // The ASP.NET Core Module names the site's physical root; .NET Runtime crashes name the process
        // path. Either way the install folder or the app's own DLL shows up in the message.
        public static bool IsAboutThisApp(string source, string message, TraySettings settings)
        {
            if (!IsRelevantSource(source) || string.IsNullOrEmpty(message))
            {
                return false;
            }
            return Contains(message, settings.InstallFolder)
                || Contains(message, "DBTracker.Web")
                || Contains(message, "IIS APPPOOL\\" + settings.AppPoolName);
        }

        public static string FirstLine(string message)
        {
            if (string.IsNullOrEmpty(message))
            {
                return "";
            }
            var line = message.Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries);
            var first = line.Length > 0 ? line[0].Trim() : "";
            return first.Length <= 160 ? first : first.Substring(0, 157) + "...";
        }

        private static bool Contains(string text, string value) =>
            !string.IsNullOrEmpty(value) && text.IndexOf(value, StringComparison.OrdinalIgnoreCase) >= 0;
    }
}
