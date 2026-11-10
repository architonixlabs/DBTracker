namespace DBTracker.Core.Enums;

/// <summary>D12 — one entry kind in a change window's cutover journal. The journal records externally performed actions; it never triggers or runs anything itself.</summary>
public enum CutoverJournalEntryType
{
    Action,
    TrafficOrWriteStateNote,
    GoNoGoDecision,
    PointOfNoReturn,
    RollbackOutcome,
    Note
}
