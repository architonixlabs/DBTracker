using DBTracker.Core.Enums;

namespace DBTracker.Core.Entities;

/// <summary>The DBA's recorded response to a support-runway deadline (upgrade, patch, replace, retire, exception). ActionReference is free text.</summary>
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
