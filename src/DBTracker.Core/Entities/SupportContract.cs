using DBTracker.Core.Enums;

namespace DBTracker.Core.Entities;

/// <summary>A support agreement reference covering one or more instances. Entitlement is computed from this table only, so it can legitimately disagree with the product's lifecycle phase.</summary>
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
