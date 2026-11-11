using DBTracker.Core.Entities;
using DBTracker.Core.Services;
using DBTracker.Infrastructure.Audit;
using Microsoft.EntityFrameworkCore;

namespace DBTracker.Infrastructure.Identity;

public record CreatedAccessToken(PersonalAccessToken Record, string Token);
public record AccessTokenUser(string UserId, string Email, IReadOnlyList<string> Roles);

/// <summary>Personal access tokens for read-only API clients. Uses short-lived contexts: validation runs inside the authentication handler.</summary>
public class PersonalAccessTokenService(IDbContextFactory<DBTrackerDbContext> factory, AuditRecorder audit)
{
    public async Task<CreatedAccessToken> CreateAsync(string userId, string userName, string name, int expiryDays)
    {
        var generated = PersonalAccessTokenCodec.Generate();
        var now = DateTime.UtcNow;
        var record = new PersonalAccessToken
        {
            UserId = userId,
            Name = name.Trim(),
            TokenHash = generated.Hash,
            DisplayPrefix = generated.DisplayPrefix,
            CreatedAtUtc = now,
            ExpiresAtUtc = now.AddDays(PersonalAccessTokenCodec.NormalizeExpiryDays(expiryDays)),
        };

        using (var db = await factory.CreateDbContextAsync())
        {
            db.PersonalAccessTokens.Add(record);
            await db.SaveChangesAsync();
        }

        await audit.RecordUserActionAsync(userId, userName, "AccessTokenCreated", "AccessToken", record.Id.ToString(), record.Name,
            reason: $"Expires {record.ExpiresAtUtc:yyyy-MM-dd}");
        return new CreatedAccessToken(record, generated.Token);
    }

    public async Task<List<PersonalAccessToken>> ListAsync(string userId)
    {
        using var db = await factory.CreateDbContextAsync();
        return await db.PersonalAccessTokens.AsNoTracking()
            .Where(t => t.UserId == userId)
            .OrderByDescending(t => t.CreatedAtUtc)
            .ToListAsync();
    }

    /// <summary>Revokes one of the user's own tokens. Returns false if it isn't theirs or is already revoked.</summary>
    public async Task<bool> RevokeAsync(string userId, string userName, int tokenId)
    {
        using var db = await factory.CreateDbContextAsync();
        var token = await db.PersonalAccessTokens.FirstOrDefaultAsync(t => t.Id == tokenId && t.UserId == userId);
        if (token is null || token.RevokedAtUtc is not null)
        {
            return false;
        }

        token.RevokedAtUtc = DateTime.UtcNow;
        token.RevokedBy = userName;
        await db.SaveChangesAsync();
        await audit.RecordUserActionAsync(userId, userName, "AccessTokenRevoked", "AccessToken", token.Id.ToString(), token.Name);
        return true;
    }

    /// <summary>Revokes every active token a user holds (on deactivation). Returns how many were revoked.</summary>
    public async Task<int> RevokeAllForUserAsync(string userId, string actorId, string actorName, string reason)
    {
        using var db = await factory.CreateDbContextAsync();
        var tokens = await db.PersonalAccessTokens.Where(t => t.UserId == userId && t.RevokedAtUtc == null).ToListAsync();
        var now = DateTime.UtcNow;
        foreach (var token in tokens)
        {
            token.RevokedAtUtc = now;
            token.RevokedBy = actorName;
        }
        if (tokens.Count > 0)
        {
            await db.SaveChangesAsync();
            await audit.RecordUserActionAsync(actorId, actorName, "AccessTokensRevokedForUser", "User", userId, userId,
                reason: $"{tokens.Count} token(s): {reason}");
        }
        return tokens.Count;
    }

    /// <summary>Returns the token's owner when the token is well formed, unexpired, unrevoked and the owner is still active; otherwise null.</summary>
    public async Task<AccessTokenUser?> ValidateAsync(string presentedToken, CancellationToken ct = default)
    {
        if (!PersonalAccessTokenCodec.IsWellFormed(presentedToken))
        {
            return null;
        }

        var hash = PersonalAccessTokenCodec.Hash(presentedToken);
        using var db = await factory.CreateDbContextAsync(ct);
        var token = await db.PersonalAccessTokens.FirstOrDefaultAsync(t => t.TokenHash == hash, ct);
        var now = DateTime.UtcNow;
        if (token is null
            || !PersonalAccessTokenCodec.Matches(presentedToken, token.TokenHash)
            || PersonalAccessTokenCodec.StateOf(token, now) != AccessTokenState.Active)
        {
            return null;
        }

        var user = await db.Users.AsNoTracking().FirstOrDefaultAsync(u => u.Id == token.UserId, ct);
        if (user is null || !user.IsActive)
        {
            return null;
        }

        if (PersonalAccessTokenCodec.ShouldRecordUse(token.LastUsedAtUtc, now))
        {
            token.LastUsedAtUtc = now;
            await db.SaveChangesAsync(ct);
        }

        var roles = await (from ur in db.UserRoles
                           join r in db.Roles on ur.RoleId equals r.Id
                           where ur.UserId == user.Id
                           select r.Name!).ToListAsync(ct);
        return new AccessTokenUser(user.Id, user.Email ?? user.UserName ?? user.Id, roles);
    }
}
