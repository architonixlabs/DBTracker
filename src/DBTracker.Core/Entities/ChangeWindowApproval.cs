using DBTracker.Core.Enums;

namespace DBTracker.Core.Entities;

/// <summary>
/// One required approval row on a D12 ChangeWindow. RequiredScope is a free-text label (e.g. "DBA",
/// "Change manager", "Application owner", "Security") checked against the acting user's roles by the
/// pure ChangeWindowApprovalAuthorizer before Decision/ApproverName/ApproverUserId can be recorded —
/// an explicit role/scope check, not just a free-text name field.
/// </summary>
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
