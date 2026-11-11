using DBTracker.Core.Enums;

namespace DBTracker.Core.Entities;

/// <summary>One validated state change on a migration campaign. Append-only history.</summary>
public class CampaignStateTransition
{
    public int Id { get; set; }

    public int MigrationCampaignId { get; set; }
    public MigrationCampaign? MigrationCampaign { get; set; }

    public CampaignState FromState { get; set; }
    public CampaignState ToState { get; set; }
    public string Reason { get; set; } = string.Empty;

    public DateTime TransitionedAtUtc { get; set; } = DateTime.UtcNow;
    public string TransitionedBy { get; set; } = string.Empty;
}
