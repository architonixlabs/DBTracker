using DBTracker.Core.Enums;

namespace DBTracker.Core.Entities;

/// <summary>A time-boxed waiver of one named gate on one target (TargetType + TargetId). It never changes vendor facts or clears the risk, and is never paid vendor coverage.</summary>
/// <remarks>Effective status, including Expired, is computed by ReadinessExceptionEvaluator; RenewalOfExceptionId keeps the renewal chain.</remarks>
public class ReadinessException
{
    public int Id { get; set; }

    /// <summary>Kind of record covered, e.g. "MigrationCampaign", "ChangeWindow", "DatabaseInstance". A string because the set of waivable targets grows.</summary>
    public string TargetType { get; set; } = string.Empty;
    public int TargetId { get; set; }

    /// <summary>Exactly which policy or gate is waived (e.g. "rehearsal evidence", "completion gate: Application tests").</summary>
    public string WaivedCheckReference { get; set; } = string.Empty;

    public string Reason { get; set; } = string.Empty;
    public string CompensatingControls { get; set; } = string.Empty;

    public int? AccountableOwnerTeamId { get; set; }
    public Team? AccountableOwnerTeam { get; set; }
    public string AccountableOwnerName { get; set; } = string.Empty;
    /// <summary>Where expiry and revocation notices are emailed, alongside the owner team's contacts.</summary>
    public string AccountableOwnerEmail { get; set; } = string.Empty;

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

    /// <summary>Set once the owner has been told this exception expired, so the sweep doesn't re-notify.</summary>
    public DateTime? ExpiryNotifiedAtUtc { get; set; }

    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;
    public string CreatedBy { get; set; } = string.Empty;
}
