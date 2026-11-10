using DBTracker.Core.Entities;
using Microsoft.EntityFrameworkCore;

namespace DBTracker.Infrastructure.Ownership;

public record TeamMemberInfo(string UserId, string DisplayName, string Email);
public record TeamSummary(Team Team, int MemberCount, int InstanceCount);

/// <summary>
/// Owner teams and their explicit memberships (U21) — the basis for "My instances" and team-scoped
/// notification subscriptions, instead of matching a user against a single free-text contact email.
/// Uses its own short-lived DbContext via the factory, same isolation pattern as every other
/// per-request lookup service in this app (NotificationService, InstanceOverviewService, …).
///
/// NOTE: membership here is personalization only — it narrows what a user sees as relevant, it
/// never grants or restricts access beyond what their ASP.NET Core Identity role already allows.
/// </summary>
public class TeamService(IDbContextFactory<DBTrackerDbContext> factory)
{
    public async Task<List<TeamSummary>> ListAsync()
    {
        using var db = await factory.CreateDbContextAsync();
        var teams = await db.Teams.AsNoTracking().OrderBy(t => t.Name).ToListAsync();
        var memberCounts = await db.TeamMemberships.AsNoTracking().GroupBy(m => m.TeamId).Select(g => new { g.Key, Count = g.Count() }).ToListAsync();
        var instanceCounts = await db.DatabaseInstances.AsNoTracking().Where(i => i.OwnerTeamId != null && !i.IsArchived)
            .GroupBy(i => i.OwnerTeamId!.Value).Select(g => new { g.Key, Count = g.Count() }).ToListAsync();

        return teams.Select(t => new TeamSummary(
            t,
            memberCounts.FirstOrDefault(m => m.Key == t.Id)?.Count ?? 0,
            instanceCounts.FirstOrDefault(i => i.Key == t.Id)?.Count ?? 0)).ToList();
    }

    /// <summary>Returns false (no-op) if a team with this name already exists.</summary>
    public async Task<bool> CreateAsync(string name, string primaryContactName, string primaryContactEmail, string backupContactName, string backupContactEmail)
    {
        using var db = await factory.CreateDbContextAsync();
        if (await db.Teams.AnyAsync(t => t.Name == name))
        {
            return false;
        }

        db.Teams.Add(new Team
        {
            Name = name,
            PrimaryContactName = primaryContactName,
            PrimaryContactEmail = primaryContactEmail,
            BackupContactName = backupContactName,
            BackupContactEmail = backupContactEmail
        });
        await db.SaveChangesAsync();
        return true;
    }

    public async Task<List<TeamMemberInfo>> ListMembersAsync(int teamId)
    {
        using var db = await factory.CreateDbContextAsync();
        var userIds = await db.TeamMemberships.AsNoTracking().Where(m => m.TeamId == teamId).Select(m => m.UserId).ToListAsync();
        if (userIds.Count == 0)
        {
            return [];
        }

        return await db.Users.AsNoTracking()
            .Where(u => userIds.Contains(u.Id))
            .Select(u => new TeamMemberInfo(u.Id, u.DisplayName, u.Email ?? ""))
            .ToListAsync();
    }

    /// <summary>Returns false (no-op) if the user is already a member of this team.</summary>
    public async Task<bool> AddMemberAsync(int teamId, string userId)
    {
        using var db = await factory.CreateDbContextAsync();
        if (await db.TeamMemberships.AnyAsync(m => m.TeamId == teamId && m.UserId == userId))
        {
            return false;
        }

        db.TeamMemberships.Add(new TeamMembership { TeamId = teamId, UserId = userId });
        await db.SaveChangesAsync();
        return true;
    }

    public async Task RemoveMemberAsync(int teamId, string userId)
    {
        using var db = await factory.CreateDbContextAsync();
        await db.TeamMemberships.Where(m => m.TeamId == teamId && m.UserId == userId).ExecuteDeleteAsync();
    }

    /// <summary>Every team id a user belongs to — what InstanceListFilter.IsMine/MyTeamIds needs for "My instances" and team-scoped subscription matching.</summary>
    public async Task<HashSet<int>> GetUserTeamIdsAsync(string userId)
    {
        using var db = await factory.CreateDbContextAsync();
        return (await db.TeamMemberships.AsNoTracking().Where(m => m.UserId == userId).Select(m => m.TeamId).ToListAsync()).ToHashSet();
    }
}
