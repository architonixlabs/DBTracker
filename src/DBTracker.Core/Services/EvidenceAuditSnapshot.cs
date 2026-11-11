using System.Text.Json;
using DBTracker.Core.Entities;
using DBTracker.Core.Enums;

namespace DBTracker.Core.Services;

/// <summary>The JSON kept in the audit trail for manual backup and restore entries and their corrections: the values as entered,
/// so an auditor can see what was stated, by whom and when, even after a later correction.</summary>
public static class EvidenceAuditSnapshot
{
    public static string Backup(BackupRecord r) => JsonSerializer.Serialize(new
    {
        OccurredAtUtc = Utc(r.TimestampUtc),
        r.OccurredTimeZone,
        Type = r.Type.ToString(),
        Status = r.Status.ToString(),
        r.SizeMb,
        r.DurationMinutes,
        r.Notes,
        Source = r.Source.ToString(),
        r.RecordedByUserName
    });

    public static string Restore(RestoreEvent r) => JsonSerializer.Serialize(new
    {
        OccurredAtUtc = Utc(r.TimestampUtc),
        r.OccurredTimeZone,
        Reason = r.Reason.ToString(),
        Outcome = r.Outcome.ToString(),
        r.PerformedBy,
        r.LinkedBackupRecordId,
        r.DurationMinutes,
        r.TargetRecoveryTimeMinutes,
        r.EvidenceReference,
        r.Notes,
        Source = r.Source.ToString(),
        r.RecordedByUserName
    });

    /// <summary>Before or after side of a backup correction.</summary>
    public static string BackupCorrectionSide(BackupStatus status, string notes) =>
        JsonSerializer.Serialize(new { Status = status.ToString(), Notes = notes });

    /// <summary>Before or after side of a restore correction.</summary>
    public static string RestoreCorrectionSide(RestoreOutcome outcome, string notes) =>
        JsonSerializer.Serialize(new { Outcome = outcome.ToString(), Notes = notes });

    /// <summary>A maintenance result as entered, with the zone the run time was stated in.</summary>
    public static string Maintenance(MaintenanceEvidenceRecord r, string enteredTimeZone) => JsonSerializer.Serialize(new
    {
        CheckType = r.CheckType.ToString(),
        r.NodeOrDeploymentLabel,
        r.ToolName,
        r.ExpectedIntervalHours,
        RanAtUtc = r.RanAtUtc is { } ran ? Utc(ran) : null,
        EnteredTimeZone = enteredTimeZone,
        Outcome = r.Outcome.ToString(),
        Source = r.Source.ToString(),
        r.SourceReference
    });

    private static string Utc(DateTime value) => DateTime.SpecifyKind(value, DateTimeKind.Utc).ToString("yyyy-MM-ddTHH:mm:ssZ");
}
