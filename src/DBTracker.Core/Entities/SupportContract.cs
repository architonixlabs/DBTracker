using DBTracker.Core.Enums;

namespace DBTracker.Core.Entities;

/// <summary>
/// D21 — one support/maintenance agreement reference, linkable to one or more products/deployments
/// via SupportContractAssignment. This records a contractual/commercial fact distinct from D02/D03's
/// vendor-lifecycle facts: EntitlementStatusClassifier reads only this table (plus "now"), never
/// ReleaseCatalogEntry/SupportRunwayDecision, so a product's lifecycle phase and its entitlement
/// status are always computed independently and can legitimately disagree. RenewalEvidenceReference
/// is a pointer (ticket/document id) — not a scanned contract upload; this app stores no contract
/// documents.
/// </summary>
public class SupportContract
{
    public int Id { get; set; }

    public string Vendor { get; set; } = string.Empty;
    public SupportCoverageType CoverageType { get; set; } = SupportCoverageType.Standard;

    public DateTime? CoverageStartUtc { get; set; }
    public DateTime? CoverageEndUtc { get; set; }

    public int? OwnerTeamId { get; set; }
    public Team? OwnerTeam { get; set; }
    public string OwnerName { get; set; } = string.Empty;

    /// <summary>Reference to renewal evidence (ticket/PO/document id) — a pointer, not an uploaded file.</summary>
    public string RenewalEvidenceReference { get; set; } = string.Empty;
    public DateTime? RenewalEvidenceRecordedAtUtc { get; set; }

    public string Notes { get; set; } = string.Empty;

    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;
    public string CreatedBy { get; set; } = string.Empty;
    public DateTime UpdatedAtUtc { get; set; } = DateTime.UtcNow;
    public string UpdatedBy { get; set; } = string.Empty;

    public ICollection<SupportContractAssignment> Assignments { get; set; } = new List<SupportContractAssignment>();
}
