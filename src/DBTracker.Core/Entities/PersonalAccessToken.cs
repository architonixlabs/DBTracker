namespace DBTracker.Core.Entities;

/// <summary>A user's API token for read-only clients such as the tray app. Only the SHA-256 hash of the token is stored.</summary>
public class PersonalAccessToken
{
    public int Id { get; set; }

    public string UserId { get; set; } = string.Empty;

    public string Name { get; set; } = string.Empty;

    /// <summary>Lowercase hex SHA-256 of the full token.</summary>
    public string TokenHash { get; set; } = string.Empty;

    /// <summary>The first characters of the token, safe to show so a user can tell tokens apart.</summary>
    public string DisplayPrefix { get; set; } = string.Empty;

    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;
    public DateTime ExpiresAtUtc { get; set; }
    public DateTime? LastUsedAtUtc { get; set; }
    public DateTime? RevokedAtUtc { get; set; }
    public string RevokedBy { get; set; } = string.Empty;
}
