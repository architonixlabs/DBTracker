using DBTracker.Core.Enums;

namespace DBTracker.Core.Entities;

/// <summary>A check this instance is expected to have on record (e.g. a weekly integrity check on every node). A requirement with no
/// matching result is reported as Missing, so a check that was never recorded can't hide behind an empty table.</summary>
public class MaintenanceCheckRequirement
{
    public int Id { get; set; }

    public int DatabaseInstanceId { get; set; }
    public DatabaseInstance? DatabaseInstance { get; set; }

    public MaintenanceCheckType CheckType { get; set; }

    /// <summary>Optional node or deployment the requirement is for. Empty means any result of this check type counts.</summary>
    public string NodeOrDeploymentLabel { get; set; } = string.Empty;

    /// <summary>How often a passing result is expected, in hours. Used when a result itself carries no interval.</summary>
    public int ExpectedIntervalHours { get; set; } = 168;

    public string Notes { get; set; } = string.Empty;

    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;
    public string CreatedBy { get; set; } = string.Empty;
}
