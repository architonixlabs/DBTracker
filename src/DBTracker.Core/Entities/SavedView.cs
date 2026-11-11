namespace DBTracker.Core.Entities;

/// <summary>A per-user bookmark of a page's filter/sort query string. Applying it re-runs the same query, so it grants no extra access.</summary>
public class SavedView
{
    public int Id { get; set; }

    public string UserId { get; set; } = string.Empty;

    /// <summary>Which page this view applies to — "Instances" or "Reports" today.</summary>
    public string ViewType { get; set; } = string.Empty;

    public string Name { get; set; } = string.Empty;

    /// <summary>The query string (without leading "?") to re-apply, e.g. "engine=SqlServer&amp;sort=Risk&amp;dir=desc".</summary>
    public string QueryString { get; set; } = string.Empty;

    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAtUtc { get; set; } = DateTime.UtcNow;
}
