using DBTracker.Core.Enums;

namespace DBTracker.Core.Entities;

/// <summary>
/// D21 — a reviewed estimate of extended-support-or-migration cost for one instance, plus the
/// reviewed target-edition feature/licensing constraints it's based on. This is commercial/cost
/// evidence: CostEvidenceMasker (and the pages that use it) restrict the amount/currency/assumptions
/// to Roles.CommercialEvidencePolicy (Admin/ITManager) — every other authenticated role sees that an
/// estimate exists but not its figures. SourceCostEstimateId on CampaignAlternative is the optional
/// link back from here into D10 ("feeds D10 as an alternative's cost estimate"); EvidenceUrl/
/// EvidenceNotes on a D14 ReadinessException can reference this row's Id as evidence for an exception
/// — neither of those is a new mechanism, both reuse what D10/D14 already have.
/// </summary>
public class EditionCostEstimate
{
    public int Id { get; set; }

    public int DatabaseInstanceId { get; set; }
    public DatabaseInstance? DatabaseInstance { get; set; }

    public int? SupportContractId { get; set; }
    public SupportContract? SupportContract { get; set; }

    public CostEstimateType EstimateType { get; set; } = CostEstimateType.ExtendedSupport;

    /// <summary>The reviewed target edition/path this estimate is based on (e.g. "SQL Server 2022 Enterprise" / "Managed PostgreSQL 16").</summary>
    public string TargetEditionOrPath { get; set; } = string.Empty;

    /// <summary>Reviewed feature/licensing constraints of the target edition — e.g. core-based licensing, feature gating, named-user minimums.</summary>
    public string LicensingConstraintsNotes { get; set; } = string.Empty;

    public decimal EstimatedAmount { get; set; }
    public string Currency { get; set; } = "USD";
    public DateTime EstimateDateUtc { get; set; } = DateTime.UtcNow;

    /// <summary>Stated assumptions behind the figure — required context, never presented as a quote or certified cost.</summary>
    public string Assumptions { get; set; } = string.Empty;

    public string ReviewedBy { get; set; } = string.Empty;
    public DateTime? ReviewedAtUtc { get; set; }

    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;
    public string CreatedBy { get; set; } = string.Empty;
}
