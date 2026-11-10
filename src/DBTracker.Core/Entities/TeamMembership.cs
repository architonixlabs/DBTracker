namespace DBTracker.Core.Entities;

/// <summary>
/// Explicit user-to-team membership (U21) — the basis for "My instances" and team-scoped
/// notification subscriptions, instead of matching a user against a raw free-text contact email.
/// Purely a personalization link: it narrows what's shown as relevant, never what the user's role
/// already allows them to see or do.
/// </summary>
public class TeamMembership
{
    public int Id { get; set; }

    public int TeamId { get; set; }
    public Team? Team { get; set; }

    /// <summary>ASP.NET Core Identity user id (ApplicationUser.Id).</summary>
    public string UserId { get; set; } = string.Empty;

    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;
}
