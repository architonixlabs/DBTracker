namespace DBTracker.Core.Entities;

/// <summary>A user's membership in a team. It drives My instances and team subscriptions, never access.</summary>
public class TeamMembership
{
    public int Id { get; set; }

    public int TeamId { get; set; }
    public Team? Team { get; set; }

    /// <summary>ASP.NET Core Identity user id (ApplicationUser.Id).</summary>
    public string UserId { get; set; } = string.Empty;

    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;
}
