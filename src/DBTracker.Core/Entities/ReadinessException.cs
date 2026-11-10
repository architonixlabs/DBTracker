using DBTracker.Core.Enums;

namespace DBTracker.Core.Entities;

/// <summary>
/// D14 — the generic, reusable exception/waiver mechanism D05/D09/D11/D12/D13 all reference: a
/// TargetType + TargetId (e.g. "MigrationCampaign"/campaignId, "RehearsalRecord"/rehearsalId,
/// "ReadinessCheckItem"/itemId, "SupportRunwayDecision"/decisionId, "ChangeWindow"/windowId) plus
/// exactly which policy/gate is being waived (WaivedCheckReference). An exception is an explicit,
/// time-boxed risk acceptance — it never changes any vendor fact (D02/D03) and never clears the
/// underlying risk; IsVendorSupportCoverage is fixed false specifically so this table is never
/// confused with actual paid vendor support entitlement (that remains a separate, later-epic concept).
/// ReadinessExceptionEvaluator (pure) computes the live effective status from Status + ExpiresAtUtc —
/// nothing here mutates Status to "Expired" on its own, the same pattern D09 uses for evidence expiry.
/// RenewalOfExceptionId preserves the rejection/renewal chain instead of overwriting history.
/// </summary>
public class ReadinessException
{
    public int Id { get; set; }

    /// <summary>What kind of record this exception covers, e.g. "MigrationCampaign", "RehearsalRecord", "ChangeWindow", "ReadinessCheckItem", "SupportRunwayDecision", "DatabaseInstance". Not an enum — the set of waivable targets grows as new features reference this table.</summary>
    public string TargetType { get; set; } = string.Empty;
    public int TargetId { get; set; }

    /// <summary>Exactly which policy/gate is being waived (e.g. "D11 rehearsal evidence", "D13 completion gate: Application tests", "D03 support-runway notice").</summary>
    public string WaivedCheckReference { get; set; } = string.Empty;

    public string Reason { get; set; } = string.Empty;
    public string CompensatingControls { get; set; } = string.Empty;

    public int? AccountableOwnerTeamId { get; set; }
    public Team? AccountableOwnerTeam { get; set; }
    public string AccountableOwnerName { get; set; } = string.Empty;

    public string PreparedByUserId { get; set; } = string.Empty;
    public string PreparedByUserName { get; set; } = string.Empty;

    /// <summary>When true, PreparedByUserName and the eventual approver must be different people (ReadinessExceptionEvaluator.CanApprove) — policy-driven separation of duties.</summary>
    public bool RequiresSeparateApprover { get; set; } = true;

    /// <summary>Role required to approve this exception (checked by ExceptionApprovalAuthorizer against the acting user's roles) — empty means Admin-only.</summary>
    public string PermittedApproverRole { get; set; } = string.Empty;

    public ReadinessExceptionStatus Status { get; set; } = ReadinessExceptionStatus.PendingApproval;
    public string ApprovedByUserId { get; set; } = string.Empty;
    public string ApprovedByUserName { get; set; } = string.Empty;
    public DateTime? DecidedAtUtc { get; set; }
    public string DecisionNotes { get; set; } = string.Empty;

    public string EvidenceUrl { get; set; } = string.Empty;
    public string EvidenceNotes { get; set; } = string.Empty;

    /// <summary>Always false — an internal waiver recorded here is never actual paid vendor support coverage.</summary>
    public bool IsVendorSupportCoverage { get; set; } = false;

    public DateTime ExpiresAtUtc { get; set; }
    public DateTime? ReviewDate { get; set; }

    public DateTime? RevokedAtUtc { get; set; }
    public string RevokedBy { get; set; } = string.Empty;
    public string RevokedReason { get; set; } = string.Empty;

    /// <summary>Set when this exception renews an earlier rejected/expired one — preserves the chain instead of overwriting it.</summary>
    public int? RenewalOfExceptionId { get; set; }
    public ReadinessException? RenewalOfException { get; set; }

    /// <summary>Dedupe state for the expiry-notice sweep (ReadinessExceptionService.EvaluateAndNotifyExpiringAsync) — set once the owner has been alerted that this exception has newly become Expired, so the same expiry isn't re-notified every sweep.</summary>
    public DateTime? ExpiryNotifiedAtUtc { get; set; }

    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;
    public string CreatedBy { get; set; } = string.Empty;
}
