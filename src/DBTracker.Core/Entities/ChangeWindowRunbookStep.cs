namespace DBTracker.Core.Entities;

/// <summary>One itemized runbook/checklist step attached to a D12 ChangeWindow. IsCompleted is a plain self-reported checkbox — it is not wired to any execution, same as the journal itself.</summary>
public class ChangeWindowRunbookStep
{
    public int Id { get; set; }

    public int ChangeWindowId { get; set; }
    public ChangeWindow? ChangeWindow { get; set; }

    public int Order { get; set; }
    public string Description { get; set; } = string.Empty;
    public bool IsRequired { get; set; } = true;
    public bool IsCompleted { get; set; }
}
