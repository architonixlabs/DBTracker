using DBTracker.Core.Entities;
using DBTracker.Core.Enums;

namespace DBTracker.Core.Services;

/// <summary>The effective value of a field with append-only corrections: the newest correction's value, or the original when there is none.</summary>
public static class EvidenceCorrectionResolver
{
    public static BackupStatus EffectiveStatus(BackupRecord record, IReadOnlyCollection<BackupRecordCorrection> corrections)
    {
        var latest = corrections
            .Where(c => c.BackupRecordId == record.Id && c.NewStatus is not null)
            .OrderByDescending(c => c.CorrectedAtUtc).ThenByDescending(c => c.Id)
            .FirstOrDefault();
        return latest?.NewStatus ?? record.Status;
    }

    public static string EffectiveNotes(BackupRecord record, IReadOnlyCollection<BackupRecordCorrection> corrections)
    {
        var latest = corrections
            .Where(c => c.BackupRecordId == record.Id && c.NewNotes is not null)
            .OrderByDescending(c => c.CorrectedAtUtc).ThenByDescending(c => c.Id)
            .FirstOrDefault();
        return latest?.NewNotes ?? record.Notes;
    }

    public static RestoreOutcome EffectiveOutcome(RestoreEvent evt, IReadOnlyCollection<RestoreEventCorrection> corrections)
    {
        var latest = corrections
            .Where(c => c.RestoreEventId == evt.Id && c.NewOutcome is not null)
            .OrderByDescending(c => c.CorrectedAtUtc).ThenByDescending(c => c.Id)
            .FirstOrDefault();
        return latest?.NewOutcome ?? evt.Outcome;
    }

    public static string EffectiveNotes(RestoreEvent evt, IReadOnlyCollection<RestoreEventCorrection> corrections)
    {
        var latest = corrections
            .Where(c => c.RestoreEventId == evt.Id && c.NewNotes is not null)
            .OrderByDescending(c => c.CorrectedAtUtc).ThenByDescending(c => c.Id)
            .FirstOrDefault();
        return latest?.NewNotes ?? evt.Notes;
    }
}
