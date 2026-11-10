using DBTracker.Core.Enums;

namespace DBTracker.Core.Entities;

/// <summary>
/// An append-only correction to a BackupRecord (U11) — the original record's Status/Notes are
/// never overwritten in place. The "effective" value of a corrected field is the newest
/// correction's New value; with none, it's the original record's own value.
/// </summary>
public class BackupRecordCorrection
{
    public int Id { get; set; }

    public int BackupRecordId { get; set; }
    public BackupRecord? BackupRecord { get; set; }

    public DateTime CorrectedAtUtc { get; set; } = DateTime.UtcNow;
    public string CorrectedByUserName { get; set; } = string.Empty;
    public string Reason { get; set; } = string.Empty;

    public BackupStatus? PreviousStatus { get; set; }
    public BackupStatus? NewStatus { get; set; }

    public string? PreviousNotes { get; set; }
    public string? NewNotes { get; set; }
}
