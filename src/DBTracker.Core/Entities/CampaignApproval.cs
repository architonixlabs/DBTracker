using DBTracker.Core.Enums;

namespace DBTracker.Core.Entities;

/// <summary>One approval or rejection in a campaign's history. Appended, never overwritten.</summary>
public class CampaignApproval
{
    public int Id { get; set; }

    public int MigrationCampaignId { get; set; }
    public MigrationCampaign? MigrationCampaign { get; set; }

    /// <summary>Free-text scope label (e.g. "DBA technical", "Application owner", "Platform"), not an enum — approval scopes vary per org/change.</summary>
    public string Scope { get; set; } = string.Empty;
    public CampaignApprovalDecision Decision { get; set; }
    public string ApproverName { get; set; } = string.Empty;
    public string Notes { get; set; } = string.Empty;

    public DateTime DecidedAtUtc { get; set; } = DateTime.UtcNow;
}
