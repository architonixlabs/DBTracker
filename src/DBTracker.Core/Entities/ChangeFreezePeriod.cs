using DBTracker.Core.Enums;

namespace DBTracker.Core.Entities;

/// <summary>An organisation-wide change freeze (quarter close, peak season). A change window that overlaps one can still be planned,
/// but can't be approved or started unless an active emergency exception is linked to it.</summary>
public class ChangeFreezePeriod
{
    public int Id { get; set; }

    public string Name { get; set; } = string.Empty;
    public DateTime StartUtc { get; set; }
    public DateTime EndUtc { get; set; }

    /// <summary>Null means the freeze covers every environment.</summary>
    public EnvironmentTier? AppliesToEnvironment { get; set; }

    public string Reason { get; set; } = string.Empty;

    /// <summary>Set when the freeze is lifted early. A cancelled freeze is kept for history and no longer conflicts.</summary>
    public DateTime? CancelledAtUtc { get; set; }
    public string CancelledBy { get; set; } = string.Empty;

    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;
    public string CreatedBy { get; set; } = string.Empty;
}
