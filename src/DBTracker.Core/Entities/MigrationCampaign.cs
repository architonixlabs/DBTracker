using DBTracker.Core.Enums;

namespace DBTracker.Core.Entities;

/// <summary>A migration wave grouping deployments under one source, target, and method. State moves only through CampaignStateTransitionValidator, and scheduling never implies reduced risk elsewhere.</summary>
public class MigrationCampaign
{
    public int Id { get; set; }

    public string Name { get; set; } = string.Empty;
    public string SourceLabel { get; set; } = string.Empty;
    public string TargetLabel { get; set; } = string.Empty;
    public string Reason { get; set; } = string.Empty;
    public MigrationMethod ChosenMethod { get; set; }

    public int? AccountableDbaTeamId { get; set; }
    public Team? AccountableDbaTeam { get; set; }
    public int? AccountableAppTeamId { get; set; }
    public Team? AccountableAppTeam { get; set; }
    public int? AccountablePlatformTeamId { get; set; }
    public Team? AccountablePlatformTeam { get; set; }

    public string Blockers { get; set; } = string.Empty;
    public string Milestones { get; set; } = string.Empty;
    public decimal? EstimatedEffortHours { get; set; }
    public decimal? EstimatedCost { get; set; }
    public string ChangeTicketReference { get; set; } = string.Empty;

    public CampaignState State { get; set; } = CampaignState.Proposed;

    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;
    public string CreatedBy { get; set; } = string.Empty;
    public DateTime UpdatedAtUtc { get; set; } = DateTime.UtcNow;

    public ICollection<CampaignInstanceLink> InstanceLinks { get; set; } = new List<CampaignInstanceLink>();
    public ICollection<CampaignAlternative> Alternatives { get; set; } = new List<CampaignAlternative>();
    public ICollection<CampaignStateTransition> StateTransitions { get; set; } = new List<CampaignStateTransition>();
    public ICollection<CampaignApproval> Approvals { get; set; } = new List<CampaignApproval>();
}
