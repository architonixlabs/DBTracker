namespace DBTracker.Core.Entities;

/// <summary>One ordered rollback decision point: a trigger and its consequence. IsPointOfNoReturn marks where the change stops being cleanly reversible.</summary>
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
