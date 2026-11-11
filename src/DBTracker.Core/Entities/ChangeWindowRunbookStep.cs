namespace DBTracker.Core.Entities;

/// <summary>One runbook step on a change window. IsCompleted is self-reported and triggers nothing.</summary>
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
