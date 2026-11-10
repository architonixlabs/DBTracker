namespace DBTracker.Core.Entities;

/// <summary>A named, per-user bookmark of a list/report's filter+sort query string (U05) — never grants access beyond what the owning user's role already allows, since applying it just re-runs the same query string through the same page.</summary>
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
