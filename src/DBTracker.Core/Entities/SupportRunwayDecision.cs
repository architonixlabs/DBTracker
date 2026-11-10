using DBTracker.Core.Enums;

namespace DBTracker.Core.Entities;

/// <summary>
/// The DBA's recorded response to a support-runway deadline on one instance (D03) — a newer
/// release or an approaching end-of-support date is information, not an automatic mandate; this is
/// where the actual chosen action (upgrade/patch/replace/retire/exception) and its reference get
/// written down. ActionReference is deliberately just free text/a reference for now — full
/// workflow entities for each action type are D08-D14 (later epic) scope.
/// </summary>
public class SupportRunwayDecision
{
    public int Id { get; set; }

    public int DatabaseInstanceId { get; set; }
    public DatabaseInstance? DatabaseInstance { get; set; }

    public int? ReleaseCatalogEntryId { get; set; }
    public ReleaseCatalogEntry? ReleaseCatalogEntry { get; set; }

    public SupportActionType ActionType { get; set; }
    public string ActionReference { get; set; } = string.Empty;
    public string Notes { get; set; } = string.Empty;

    public string DecidedBy { get; set; } = string.Empty;
    public DateTime DecidedAtUtc { get; set; } = DateTime.UtcNow;
}
