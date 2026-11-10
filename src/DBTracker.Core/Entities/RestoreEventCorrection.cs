using DBTracker.Core.Enums;

namespace DBTracker.Core.Entities;

/// <summary>
/// An append-only correction to a RestoreEvent (U11) — same model as BackupRecordCorrection. The
/// original record's Outcome/Notes are never overwritten in place.
/// </summary>
public class RestoreEventCorrection
{
    public int Id { get; set; }

    public int RestoreEventId { get; set; }
    public RestoreEvent? RestoreEvent { get; set; }

    public DateTime CorrectedAtUtc { get; set; } = DateTime.UtcNow;
    public string CorrectedByUserName { get; set; } = string.Empty;
    public string Reason { get; set; } = string.Empty;

    public RestoreOutcome? PreviousOutcome { get; set; }
    public RestoreOutcome? NewOutcome { get; set; }

    public string? PreviousNotes { get; set; }
    public string? NewNotes { get; set; }
}
