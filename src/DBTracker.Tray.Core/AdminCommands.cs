using System;
using System.Collections.Generic;

namespace DBTracker.Tray.Core
{
    public enum AdminAction
    {
        StartAppPool,
        StopAppPool,
        RestartAppPool,
        StartSite,
        StopSite,
        CopySetupKey,
        RestartSite,
    }

    // The elevated copy of the tray exe receives only the action name; it reads site and pool names
    // from the registry itself, so nothing user-controlled reaches appcmd.exe through arguments.
    public static class AdminCommands
    {
        public const string Switch = "--admin";

        private static readonly Dictionary<AdminAction, string> Names = new Dictionary<AdminAction, string>
        {
            [AdminAction.StartAppPool] = "start-pool",
            [AdminAction.StopAppPool] = "stop-pool",
            [AdminAction.RestartAppPool] = "restart-pool",
            [AdminAction.StartSite] = "start-site",
            [AdminAction.StopSite] = "stop-site",
            [AdminAction.CopySetupKey] = "copy-setup-key",
            [AdminAction.RestartSite] = "restart-site",
        };

        public static string ToArgument(AdminAction action) => Names[action];

        public static bool TryParse(string argument, out AdminAction action)
        {
            foreach (var pair in Names)
            {
                if (string.Equals(pair.Value, argument, StringComparison.OrdinalIgnoreCase))
                {
                    action = pair.Key;
                    return true;
                }
            }
            action = default(AdminAction);
            return false;
        }

        // appcmd.exe argument lists to run in order; restart is stop then start because recycling a
        // stopped pool fails.
        public static IReadOnlyList<string> AppCmdArguments(AdminAction action, TraySettings settings)
        {
            if (!TraySettings.IsSafeName(settings.AppPoolName) || !TraySettings.IsSafeName(settings.SiteName))
            {
                throw new ArgumentException("Site or app pool name contains characters that are not allowed.");
            }

            var pool = "/apppool.name:\"" + settings.AppPoolName + "\"";
            var site = "/site.name:\"" + settings.SiteName + "\"";
            switch (action)
            {
                case AdminAction.StartAppPool: return new[] { "start apppool " + pool };
                case AdminAction.StopAppPool: return new[] { "stop apppool " + pool };
                case AdminAction.RestartAppPool: return new[] { "stop apppool " + pool, "start apppool " + pool };
                case AdminAction.StartSite: return new[] { "start site " + site };
                case AdminAction.StopSite: return new[] { "stop site " + site };
                case AdminAction.RestartSite: return new[] { "stop site " + site, "start site " + site };
                default: return new string[0];
            }
        }

        public static string Describe(AdminAction action)
        {
            switch (action)
            {
                case AdminAction.StartAppPool: return "Start the app pool";
                case AdminAction.StopAppPool: return "Stop the app pool";
                case AdminAction.RestartAppPool: return "Restart the app pool";
                case AdminAction.StartSite: return "Start the site";
                case AdminAction.StopSite: return "Stop the site";
                case AdminAction.RestartSite: return "Restart the site";
                case AdminAction.CopySetupKey: return "Copy the setup key";
                default: return action.ToString();
            }
        }
    }
}
