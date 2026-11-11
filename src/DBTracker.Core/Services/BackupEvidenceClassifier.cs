using DBTracker.Core.Entities;
using DBTracker.Core.Enums;

namespace DBTracker.Core.Services;

/// <summary>How strongly a backup is verified: a discovered file is weaker than a reported success, which is weaker than a proven restore.</summary>
public static class BackupEvidenceClassifier
{
    /// <summary>Null for Failed/Missed records; evidence level only describes claimed-successful backups.</summary>
    public static EvidenceLevel? Classify(BackupRecord record, IReadOnlyCollection<RestoreEvent> restoreEvents)
    {
        if (record.Status != BackupStatus.Success)
        {
            return null;
        }

        var provenRestorable = restoreEvents.Any(r =>
            r.Outcome == RestoreOutcome.Success && r.TimestampUtc >= record.TimestampUtc);

        if (provenRestorable)
        {
            return EvidenceLevel.RestoreTested;
        }

        // A discovered file is never upgraded to Reported just because nothing was restored from it yet.
        return record.Source == BackupSource.Discovered
            ? EvidenceLevel.Discovered
            : EvidenceLevel.ReportedSuccess;
    }

    public static string Describe(EvidenceLevel level) => level switch
    {
        EvidenceLevel.Discovered => "Discovered",
        EvidenceLevel.ReportedSuccess => "Reported success",
        EvidenceLevel.RestoreTested => "Restore-tested",
        _ => level.ToString()
    };
}
