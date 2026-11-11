using DBTracker.Core.Enums;

namespace DBTracker.Core.Entities;

/// <summary>A reviewer-approved target build for a product family, kept separate from ReleaseCatalogEntry so approved is never read as newest GA. Expired baselines stay for history.</summary>
public class ApprovedPatchBaseline
{
    public int Id { get; set; }

    public DatabaseEngine Engine { get; set; }
    public string Family { get; set; } = string.Empty;

    public int TargetReleaseCatalogEntryId { get; set; }
    public ReleaseCatalogEntry? TargetReleaseCatalogEntry { get; set; }

    public string ReviewedBy { get; set; } = string.Empty;
    public DateTime ReviewedAtUtc { get; set; } = DateTime.UtcNow;
    public DateTime? ExpiresAtUtc { get; set; }
    public string Notes { get; set; } = string.Empty;

    public bool IsActive { get; set; } = true;

    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAtUtc { get; set; } = DateTime.UtcNow;
}
