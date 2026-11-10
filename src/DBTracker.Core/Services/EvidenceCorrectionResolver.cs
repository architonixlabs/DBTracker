using DBTracker.Core.Entities;
using DBTracker.Core.Enums;

namespace DBTracker.Core.Services;

/// <summary>
/// Resolves the "effective" value of a field that may have append-only corrections layered over it
/// (U11). The original record is never mutated — effective value is the newest correction's New
/// value for that field, falling back to the record's own original value when there's no correction.
/// Pure (no I/O).
/// </summary>
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
