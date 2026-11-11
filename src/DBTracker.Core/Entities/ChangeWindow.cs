using DBTracker.Core.Enums;

namespace DBTracker.Core.Entities;

/// <summary>A planned, timezone-labelled change window. Creating one executes nothing and sends nothing; it reaches Approved only through ChangeWindowService's approval gate.</summary>
public class ChangeWindow
{
    public int Id { get; set; }

    public int MigrationCampaignId { get; set; }
    public MigrationCampaign? MigrationCampaign { get; set; }

    public string Title { get; set; } = string.Empty;
    public DateTime PlannedStartUtc { get; set; }
    public DateTime PlannedEndUtc { get; set; }
    /// <summary>Free-text timezone label the window was planned/communicated in (e.g. "America/New_York", "UTC+5:30") — PlannedStart/EndUtc remain the stored UTC instants either way.</summary>
    public string TimeZoneLabel { get; set; } = string.Empty;

    public int? OutageEstimateMinutes { get; set; }

    public int? CommunicationOwnerTeamId { get; set; }
    public Team? CommunicationOwnerTeam { get; set; }

    /// <summary>Runbook/checklist narrative in addition to the itemized RunbookSteps below.</summary>
    public string RunbookNotes { get; set; } = string.Empty;

    public ChangeWindowStatus Status { get; set; } = ChangeWindowStatus.Planned;

    /// <summary>An emergency exception that lets this window proceed without every normal approval. Never set implicitly.</summary>
    public int? EmergencyExceptionId { get; set; }
    public ReadinessException? EmergencyException { get; set; }

    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;
    public string CreatedBy { get; set; } = string.Empty;
    public DateTime UpdatedAtUtc { get; set; } = DateTime.UtcNow;

    public ICollection<ChangeWindowRunbookStep> RunbookSteps { get; set; } = new List<ChangeWindowRunbookStep>();
    public ICollection<ChangeWindowApproval> Approvals { get; set; } = new List<ChangeWindowApproval>();
    public ICollection<CutoverJournalEntry> JournalEntries { get; set; } = new List<CutoverJournalEntry>();
}
