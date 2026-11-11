using DBTracker.Core.Enums;

namespace DBTracker.Core.Entities;

/// <summary>A reviewed extended-support or migration cost estimate and the target-edition constraints it assumes. Figures are visible only to CommercialEvidencePolicy roles.</summary>
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
