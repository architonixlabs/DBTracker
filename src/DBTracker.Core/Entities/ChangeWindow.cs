using DBTracker.Core.Enums;

namespace DBTracker.Core.Entities;

/// <summary>
/// D12 — a planned, future, timezone-labelled change window for a D10 MigrationCampaign. Creating
/// this record is purely a planning fact: it never executes anything and never sends any
/// notification by itself (no code path here calls IEmailSender/IWebhookSender/NotificationService).
/// Status only ever advances via ChangeWindowService, which checks ChangeWindowApprovalGate (all
/// required ChangeWindowApprovals recorded by an authorized approver, or an active linked
/// EmergencyException) before allowing Approved, mirroring how D10's CampaignStateTransitionValidator
/// gates MigrationCampaign.State.
/// </summary>
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

    /// <summary>A recorded emergency exception (D14) that lets this window proceed without every normal approval having been recorded — never set implicitly.</summary>
    public int? EmergencyExceptionId { get; set; }
    public ReadinessException? EmergencyException { get; set; }

    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;
    public string CreatedBy { get; set; } = string.Empty;
    public DateTime UpdatedAtUtc { get; set; } = DateTime.UtcNow;

    public ICollection<ChangeWindowRunbookStep> RunbookSteps { get; set; } = new List<ChangeWindowRunbookStep>();
    public ICollection<ChangeWindowApproval> Approvals { get; set; } = new List<ChangeWindowApproval>();
    public ICollection<CutoverJournalEntry> JournalEntries { get; set; } = new List<CutoverJournalEntry>();
}
