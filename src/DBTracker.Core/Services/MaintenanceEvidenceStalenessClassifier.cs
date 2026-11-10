using DBTracker.Core.Entities;
using DBTracker.Core.Enums;

namespace DBTracker.Core.Services;

/// <summary>
/// D18 — pure staleness/outcome classification for one MaintenanceEvidenceRecord, kept on its own
/// axis from BackupHealth (U04's "separate concerns" pattern) and always describing a recorded,
/// point-in-time result rather than continuously measured HA health. A Failed outcome always wins
/// regardless of how recently it ran; a Passed outcome with no recorded schedule stays Unknown rather
/// than being assumed Current.
/// </summary>
public static class MaintenanceEvidenceStalenessClassifier
{
    public static MaintenanceEvidenceStatus Classify(MaintenanceEvidenceRecord record, DateTime nowUtc)
    {
        if (record.RanAtUtc is null)
        {
            return MaintenanceEvidenceStatus.Missing;
        }

        if (record.Outcome == MaintenanceOutcome.Failed)
        {
            return MaintenanceEvidenceStatus.Failed;
        }

        if (record.Outcome == MaintenanceOutcome.Unknown || record.ExpectedIntervalHours is null)
        {
            return MaintenanceEvidenceStatus.Unknown;
        }

        var dueByUtc = record.RanAtUtc.Value.AddHours(record.ExpectedIntervalHours.Value);
        return dueByUtc <= nowUtc ? MaintenanceEvidenceStatus.Stale : MaintenanceEvidenceStatus.Current;
    }
}
