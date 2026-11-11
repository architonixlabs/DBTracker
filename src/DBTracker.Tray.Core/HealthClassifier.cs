using System;

namespace DBTracker.Tray.Core
{
    public static class HealthClassifier
    {
        public static HealthState Classify(HealthProbe probe, int port)
        {
            if (probe == null || probe.HttpStatus == null)
            {
                return new HealthState(StatusLight.Red, "Not responding on port " + port, false);
            }

            // A DBTracker build older than /healthz answers 404 for it but is clearly up.
            if (probe.HttpStatus == 404)
            {
                return new HealthState(StatusLight.Amber, "Running (this version has no health check)", false);
            }

            if (probe.HttpStatus != 200)
            {
                return new HealthState(StatusLight.Red, "Site returned HTTP " + probe.HttpStatus, false);
            }

            var report = TrayJson.ParseHealth(probe.Body);
            if (report == null)
            {
                return new HealthState(StatusLight.Red, "Unreadable health check reply", false);
            }

            switch ((report.Status ?? "").Trim().ToLowerInvariant())
            {
                case "ok":
                    return new HealthState(StatusLight.Green, "Running", false);
                case "setup-required":
                    return new HealthState(StatusLight.Amber, "Setup not finished", true);
                case "degraded":
                    return new HealthState(StatusLight.Amber, DegradedText(report.Database), false);
                default:
                    return new HealthState(StatusLight.Red, "Unknown status \"" + report.Status + "\"", false);
            }
        }

        private static string DegradedText(string database)
        {
            return string.Equals(database, "unreachable", StringComparison.OrdinalIgnoreCase)
                ? "Running, but the database is unreachable"
                : "Running with problems";
        }
    }
}
