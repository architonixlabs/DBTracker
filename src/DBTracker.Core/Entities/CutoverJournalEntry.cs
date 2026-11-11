using DBTracker.Core.Enums;

namespace DBTracker.Core.Entities;

/// <summary>An append-only cutover journal entry recording what a person says happened. Adding one runs nothing against any database.</summary>
public class CutoverJournalEntry
{
    public int Id { get; set; }

    public int ChangeWindowId { get; set; }
    public ChangeWindow? ChangeWindow { get; set; }

    public CutoverJournalEntryType EntryType { get; set; } = CutoverJournalEntryType.Note;
    public DateTime TimestampUtc { get; set; } = DateTime.UtcNow;
    public string Description { get; set; } = string.Empty;
    /// <summary>Traffic/write-state observation attached to this entry (e.g. "writes still flowing to source", "application pointed at target") — free text, optional.</summary>
    public string TrafficOrWriteStateNote { get; set; } = string.Empty;
    public GoNoGoDecision Decision { get; set; } = GoNoGoDecision.None;

    public string RecordedBy { get; set; } = string.Empty;
    public DateTime RecordedAtUtc { get; set; } = DateTime.UtcNow;
}
