using DBTracker.Core.Enums;

namespace DBTracker.Core.Entities;

/// <summary>One recorded state change on a D10 MigrationCampaign, validated against CampaignStateTransitionValidator before being written — the campaign's full history, append-only.</summary>
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
