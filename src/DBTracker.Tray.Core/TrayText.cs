using System.Collections.Generic;

namespace DBTracker.Tray.Core
{
    public static class TrayText
    {
        // NotifyIcon tooltips are limited to 63 characters.
        public const int MaxTooltipLength = 63;

        public static string Tooltip(HealthState state)
        {
            var text = "DBTracker: " + (state?.Text ?? "checking");
            return text.Length <= MaxTooltipLength ? text : text.Substring(0, MaxTooltipLength - 3) + "...";
        }

        public static string LightName(StatusLight light)
        {
            switch (light)
            {
                case StatusLight.Green: return "running";
                case StatusLight.Amber: return "needs attention";
                case StatusLight.Red: return "down";
                default: return "unknown";
            }
        }

        public static string Backups(TraySummary summary)
        {
            if (summary == null)
            {
                return "Backups: not connected";
            }
            return "Backups: " + summary.Failed + " failed, " + summary.Overdue + " overdue, "
                + summary.NeverBackedUp + " never backed up";
        }

        public static string TransitionTitle(LightTransition transition)
        {
            return "DBTracker is " + LightName(transition.To);
        }

        public static string NewlyAtRisk(IReadOnlyList<AtRiskInstance> added)
        {
            if (added == null || added.Count == 0)
            {
                return null;
            }
            if (added.Count == 1)
            {
                var one = added[0];
                return one.Name + " is now " + (one.Health ?? "at risk")
                    + (string.IsNullOrEmpty(one.Environment) ? "" : " (" + one.Environment + ")");
            }
            return added.Count + " instances are newly at risk, including " + added[0].Name;
        }
    }
}
