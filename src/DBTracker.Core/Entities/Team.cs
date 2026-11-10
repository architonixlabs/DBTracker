namespace DBTracker.Core.Entities;

/// <summary>
/// An owning team (U21) — a reusable alternative/complement to a single free-text contact on a
/// DatabaseInstance. Primary/backup contact fields here are a team-level default; an instance can
/// still carry its own BackupContact* fields for an instance-specific backup contact.
///
/// NOTE: a Team is a personalization/grouping concept only. Membership narrows what a user sees as
/// "mine" and which broadcast alerts reach them — it never grants or restricts access beyond what
/// their ASP.NET Core Identity role already allows. This is not row-level security.
/// </summary>
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
