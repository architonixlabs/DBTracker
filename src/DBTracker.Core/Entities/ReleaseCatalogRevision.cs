namespace DBTracker.Core.Entities;

/// <summary>Append-only revision history for a catalogue entry: who changed it and a JSON snapshot of the prior state.</summary>
public class ReleaseCatalogRevision
{
    public int Id { get; set; }

    public int ReleaseCatalogEntryId { get; set; }
    public ReleaseCatalogEntry? ReleaseCatalogEntry { get; set; }

    public DateTime RevisedAtUtc { get; set; } = DateTime.UtcNow;
    public string RevisedBy { get; set; } = string.Empty;
    public string ChangeSummary { get; set; } = string.Empty;

    /// <summary>JSON snapshot of the entry's fields immediately before this revision was applied.</summary>
    public string SnapshotJson { get; set; } = string.Empty;
}
