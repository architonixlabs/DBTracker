namespace DBTracker.Core.Enums;

/// <summary>Kind of cutover journal entry. The journal records actions taken elsewhere; it never runs anything.</summary>
public enum CutoverJournalEntryType
{
    Action,
    TrafficOrWriteStateNote,
    GoNoGoDecision,
    PointOfNoReturn,
    RollbackOutcome,
    Note
}
