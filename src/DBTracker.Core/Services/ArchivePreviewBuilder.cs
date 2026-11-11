namespace DBTracker.Core.Services;

/// <summary>Counts for the archive preview, gathered by the caller.</summary>
public record ArchiveImpactCounts(
    bool BackgroundScanConfigured,
    bool AlertableNow,
    int OpenIncidentCount,
    bool IncludedInScheduledReports,
    int ScheduledReportIntervalDays = 0,
    bool DistributionListConfigured = false,
    int SavedSnapshotCount = 0);

public record ArchiveImpactLine(string Text);

/// <summary>Turns the counts into the plain-language preview of what archiving affects, shown before the archive is confirmed.</summary>
public static class ArchivePreviewBuilder
{
    public static IReadOnlyList<ArchiveImpactLine> Build(ArchiveImpactCounts counts)
    {
        var lines = new List<ArchiveImpactLine>();

        lines.Add(counts.BackgroundScanConfigured
            ? new ArchiveImpactLine("The scheduled backup-file scan for this instance will stop running. Scan history is kept, not deleted.")
            : new ArchiveImpactLine("No backup-file scan is configured for this instance, so nothing changes there."));

        lines.Add(counts.AlertableNow
            ? new ArchiveImpactLine("This instance is currently alertable. Archiving it immediately stops future Overdue/Failed email and webhook alerts.")
            : new ArchiveImpactLine("This instance isn't currently alertable, so archiving has no immediate effect on alerting."));

        lines.Add(counts.OpenIncidentCount switch
        {
            0 => new ArchiveImpactLine("No open incident will be affected."),
            1 => new ArchiveImpactLine("1 open incident exists for this instance. It is preserved but stops escalating once the instance is archived."),
            var n => new ArchiveImpactLine($"{n} open incidents exist for this instance. They are preserved but stop escalating once the instance is archived.")
        });

        if (!counts.IncludedInScheduledReports)
        {
            lines.Add(new ArchiveImpactLine("This instance is already excluded from the current reporting scope."));
        }
        else
        {
            var scheduled = counts.ScheduledReportIntervalDays <= 0
                ? "Scheduled report emails are turned off, so no email changes."
                : counts.DistributionListConfigured
                    ? $"It is also in the scheduled report emailed every {counts.ScheduledReportIntervalDays} day(s) and drops out from the next send."
                    : "Scheduled reports are on but have no distribution list, so no email changes.";
            lines.Add(new ArchiveImpactLine($"It appears in the Compliance view and Reports today and drops out of new reports once archived. {scheduled}"));
        }

        lines.Add(counts.SavedSnapshotCount switch
        {
            0 => new ArchiveImpactLine("No saved Compliance snapshot includes it."),
            1 => new ArchiveImpactLine("1 saved Compliance snapshot includes it. Saved snapshots keep their frozen rows."),
            var n => new ArchiveImpactLine($"{n} saved Compliance snapshots include it. Saved snapshots keep their frozen rows.")
        });

        return lines;
    }
}
