using DBTracker.Core.Entities;
using Microsoft.EntityFrameworkCore;

namespace DBTracker.Infrastructure.Ownership;

public record TeamMemberInfo(string UserId, string DisplayName, string Email);
public record TeamSummary(Team Team, int MemberCount, int InstanceCount);

/// <summary>Teams and memberships, which drive My instances and team subscriptions. Uses a short-lived DbContext per call.</summary>
/// <remarks>Membership is personalization only; it never grants or restricts access beyond the user's role.</remarks>
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

    /// <summary>Every team id a user belongs to, for My instances and team subscription matching.</summary>
    public async Task<HashSet<int>> GetUserTeamIdsAsync(string userId)
    {
        using var db = await factory.CreateDbContextAsync();
        return (await db.TeamMemberships.AsNoTracking().Where(m => m.UserId == userId).Select(m => m.TeamId).ToListAsync()).ToHashSet();
    }
}
