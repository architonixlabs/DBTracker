namespace DBTracker.Core.Enums;

/// <summary>D12 — a go/no-go decision recorded in a cutover journal entry. None means no decision is attached to that particular entry (e.g. a plain action/note).</summary>
public enum GoNoGoDecision
{
    None,
    Go,
    NoGo
}
