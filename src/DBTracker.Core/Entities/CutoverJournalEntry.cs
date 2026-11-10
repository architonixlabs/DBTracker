using DBTracker.Core.Enums;

namespace DBTracker.Core.Entities;

/// <summary>
/// One append-only entry in a D12 ChangeWindow's cutover journal — an externally performed action,
/// traffic/write-state note, go/no-go decision, the point of no return, or a rollback outcome, with
/// its own timestamp. This table only ever records what a human says happened; nothing in this app
/// runs a command against any tracked database as a result of adding a row here.
/// </summary>
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
