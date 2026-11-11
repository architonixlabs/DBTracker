using DBTracker.Core.Entities;
using DBTracker.Core.Enums;

namespace DBTracker.Core.Services;

/// <summary>How strongly a backup is verified: a discovered file is weaker than a reported success, which is weaker than a proven restore.</summary>
public static class BackupEvidenceClassifier
{
    /// <summary>Null for Failed/Missed records; evidence level only describes claimed-successful backups.</summary>
    /// <param name="correctionsKnownAsOfUtc">For an as-of view: only restore corrections made by then apply.</param>
    public static EvidenceLevel? Classify(BackupRecord record, IReadOnlyCollection<RestoreEvent> restoreEvents, DateTime? correctionsKnownAsOfUtc = null)
    {
        // Only verified restores (successful after corrections, not imported) can upgrade a backup to restore-tested.
        var lastRestoreSuccessUtc = RestoreEvidence.LastVerifiedUtc(restoreEvents, correctionsKnownAsOfUtc);
        return Classify(record.Status, record.Source, record.TimestampUtc, lastRestoreSuccessUtc);
    }

    /// <summary>Same rule from flat values, for queries that fetch only the instance's latest successful restore time.</summary>
    public static EvidenceLevel? Classify(BackupStatus status, BackupSource source, DateTime recordTimestampUtc, DateTime? lastRestoreSuccessUtc)
    {
        if (status != BackupStatus.Success)
        {
            return null;
        }

        // Imported history is context only: it is never upgraded, and an imported restore never proves anything.
        if (source == BackupSource.Imported)
        {
            return EvidenceLevel.Imported;
        }

        if (lastRestoreSuccessUtc is { } restoredAt && restoredAt >= recordTimestampUtc)
        {
            return EvidenceLevel.RestoreTested;
        }

        // A discovered file is never upgraded to Reported just because nothing was restored from it yet.
        return source == BackupSource.Discovered
            ? EvidenceLevel.Discovered
            : EvidenceLevel.ReportedSuccess;
    }

    /// <summary>Evidence level of the latest successful backup at or before <paramref name="asOfUtc"/>, using only restores up to that instant.
    /// Null when there is no successful backup. Ties on timestamp break by Id, the same rule BackupHealthCalculator uses.</summary>
    public static EvidenceLevel? ClassifyLatestSuccess(IReadOnlyCollection<BackupRecord> records, IReadOnlyCollection<RestoreEvent> restoreEvents, DateTime asOfUtc)
    {
        var latest = records
            .Where(r => r.Status == BackupStatus.Success && r.TimestampUtc <= asOfUtc)
            .OrderByDescending(r => r.TimestampUtc)
            .ThenByDescending(r => r.Id)
            .FirstOrDefault();

        return latest is null
            ? null
            : Classify(latest, restoreEvents.Where(r => r.TimestampUtc <= asOfUtc).ToList(), asOfUtc);
    }

    /// <summary>Label for a CSV cell or report line; empty when there is no successful backup to describe.</summary>
    public static string DescribeOrEmpty(EvidenceLevel? level) => level is { } l ? Describe(l) : "";

    public static string Describe(EvidenceLevel level) => level switch
    {
        EvidenceLevel.Discovered => "Discovered",
        EvidenceLevel.ReportedSuccess => "Reported success",
        EvidenceLevel.RestoreTested => "Restore-tested",
        EvidenceLevel.Imported => "Imported",
        _ => level.ToString()
    };
}
