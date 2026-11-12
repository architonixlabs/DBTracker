namespace DBTracker.Core.Services;

/// <summary>The payload of the one-time sign-in refresh after a password change: the user id and the security stamp it was issued for.
/// The web layer encrypts and time-limits it; this only formats and parses.</summary>
public static class SignInRefreshToken
{
    private const char Separator = '\n';

    public static string Format(string userId, string securityStamp)
    {
        if (string.IsNullOrEmpty(userId) || userId.Contains(Separator) || string.IsNullOrEmpty(securityStamp) || securityStamp.Contains(Separator))
        {
            throw new ArgumentException("A user id and security stamp without line breaks are required.");
        }
        return userId + Separator + securityStamp;
    }

    public static (string UserId, string SecurityStamp)? Parse(string? payload)
    {
        var parts = (payload ?? "").Split(Separator);
        return parts.Length == 2 && parts[0].Length > 0 && parts[1].Length > 0 ? (parts[0], parts[1]) : null;
    }
}
