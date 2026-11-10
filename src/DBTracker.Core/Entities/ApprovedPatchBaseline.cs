using DBTracker.Core.Enums;

namespace DBTracker.Core.Entities;

/// <summary>
/// An internal, reviewed organizational target for one product family (D04) — deliberately a
/// separate entity from ReleaseCatalogEntry (vendor fact) so "approved" is never conflated with
/// "available" or "newest GA". Only ever set explicitly by a reviewer; PatchBaselineComparer never
/// invents one from the newest GA release on its own. Expired/inactive baselines stop being
/// recommended but are preserved for history.
/// </summary>
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
