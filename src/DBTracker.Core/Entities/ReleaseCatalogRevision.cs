namespace DBTracker.Core.Entities;

/// <summary>Append-only revision history for a ReleaseCatalogEntry (D02) — created/edit/override/refresh, each with who and a JSON snapshot of the prior state, so a catalogue fact's history survives later edits.</summary>
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
