namespace DBTracker.Core.Entities;

/// <summary>An owning team with team-level default contacts.</summary>
/// <remarks>Grouping and personalization only: membership never grants or restricts access beyond the user's role. Not row-level security.</remarks>
public class Team
{
    public int Id { get; set; }

    public string Name { get; set; } = string.Empty;

    public string PrimaryContactName { get; set; } = string.Empty;
    public string PrimaryContactEmail { get; set; } = string.Empty;
    public string BackupContactName { get; set; } = string.Empty;
    public string BackupContactEmail { get; set; } = string.Empty;

    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;

    public ICollection<TeamMembership> Memberships { get; set; } = new List<TeamMembership>();
}
