using DBTracker.Core.Enums;

namespace DBTracker.Core.Entities;

/// <summary>One required approval on a change window. RequiredScope is checked against the approver's roles before a decision can be recorded.</summary>
public class ChangeWindowApproval
{
    public int Id { get; set; }

    public int ChangeWindowId { get; set; }
    public ChangeWindow? ChangeWindow { get; set; }

    public string RequiredScope { get; set; } = string.Empty;

    public CampaignApprovalDecision? Decision { get; set; }
    public string ApproverName { get; set; } = string.Empty;
    public string ApproverUserId { get; set; } = string.Empty;
    public string Notes { get; set; } = string.Empty;
    public DateTime? DecidedAtUtc { get; set; }

    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;
}
