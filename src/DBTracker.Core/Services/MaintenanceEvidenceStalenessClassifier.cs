using DBTracker.Core.Entities;
using DBTracker.Core.Enums;

namespace DBTracker.Core.Services;

/// <summary>Staleness and outcome of one recorded maintenance result, separate from backup health. Failed always wins; a Passed result with no schedule stays Unknown, not Current.</summary>
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
