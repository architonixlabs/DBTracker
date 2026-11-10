using DBTracker.Core.Entities;
using DBTracker.Core.Enums;

namespace DBTracker.Core.Services;

/// <summary>
/// Classifies how strongly a backup record's existence is actually verified (U01): a file found by
/// the scanner is weaker evidence than a reported success, which is itself weaker than proof the
/// chain actually restores. Pure (no I/O) so the same classification is usable in the UI, alerts
/// and reports without drifting.
/// </summary>
public static class BackupEvidenceClassifier
{
    /// <summary>
    /// Null for a record that isn't a successful backup at all (Failed/Missed) — evidence level only
    /// describes the strength of proof behind a claimed-successful backup.
    /// </summary>
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

        // Existing Discovered records keep their provenance — a later restore proves the chain
        // overall, but a file found on disk is never silently upgraded to "reported" just because
        // nothing was restored from it yet.
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
