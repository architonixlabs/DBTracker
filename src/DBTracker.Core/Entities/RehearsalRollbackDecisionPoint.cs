namespace DBTracker.Core.Entities;

/// <summary>One documented rollback decision point within a D11 rehearsal — a trigger condition and what happens if it fires, in order. IsPointOfNoReturn flags the step past which the rehearsal (and, when carried into the real D12 cutover journal, the actual cutover) is understood to no longer be cleanly reversible.</summary>
public class RehearsalRollbackDecisionPoint
{
    public int Id { get; set; }

    public int RehearsalRecordId { get; set; }
    public RehearsalRecord? RehearsalRecord { get; set; }

    public int Order { get; set; }
    public string TriggerCondition { get; set; } = string.Empty;
    public string Decision { get; set; } = string.Empty;
    public bool IsPointOfNoReturn { get; set; }
}
