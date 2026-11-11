namespace DBTracker.Core.Services;

/// <summary>Counts for the archive preview, gathered by the caller.</summary>
public record ArchiveImpactCounts(
    bool BackgroundScanConfigured,
    bool AlertableNow,
    int OpenIncidentCount,
    bool IncludedInScheduledReports);

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

        lines.Add(counts.IncludedInScheduledReports
            ? new ArchiveImpactLine("This instance currently appears in reports and the Compliance view. It drops out of new reports once archived; past reports and snapshots are unaffected.")
            : new ArchiveImpactLine("This instance is already excluded from the current reporting scope."));

        return lines;
    }
}
