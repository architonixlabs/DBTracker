using System.Security.Cryptography;
using System.Text;
using DBTracker.Core.Entities;

namespace DBTracker.Core.Services;

public record GeneratedAccessToken(string Token, string Hash, string DisplayPrefix);

public enum AccessTokenState
{
    Active,
    Expired,
    Revoked
}

/// <summary>Creates, hashes and checks personal access tokens (<c>dbt_</c> + base64url of 32 random bytes).</summary>
public static class PersonalAccessTokenCodec
{
    public const string Prefix = "dbt_";
    public const int RandomByteCount = 32;
    public const int DefaultExpiryDays = 90;
    public static readonly IReadOnlyList<int> ExpiryChoicesDays = [30, 90, 180, 365];

    // 32 bytes encode to 43 base64url characters without padding.
    private const int EncodedLength = 43;
    private const int DisplayPrefixLength = 10;
    private static readonly TimeSpan LastUsedResolution = TimeSpan.FromMinutes(1);

    public static GeneratedAccessToken Generate()
    {
        var token = Prefix + Base64UrlEncode(RandomNumberGenerator.GetBytes(RandomByteCount));
        return new GeneratedAccessToken(token, Hash(token), token[..DisplayPrefixLength]);
    }

    public static string Hash(string token) =>
        Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(token))).ToLowerInvariant();

    public static bool IsWellFormed(string? token)
    {
        if (token is null || token.Length != Prefix.Length + EncodedLength || !token.StartsWith(Prefix, StringComparison.Ordinal))
        {
            return false;
        }
        foreach (var c in token.AsSpan(Prefix.Length))
        {
            if (!(char.IsAsciiLetterOrDigit(c) || c == '-' || c == '_'))
            {
                return false;
            }
        }
        return true;
    }

    /// <summary>Constant-time comparison of the presented token's hash with a stored hash.</summary>
    public static bool Matches(string presentedToken, string storedHash)
    {
        var presented = Encoding.ASCII.GetBytes(Hash(presentedToken));
        var stored = Encoding.ASCII.GetBytes(storedHash ?? string.Empty);
        return CryptographicOperations.FixedTimeEquals(presented, stored);
    }

    public static AccessTokenState StateOf(PersonalAccessToken token, DateTime nowUtc) =>
        token.RevokedAtUtc is not null ? AccessTokenState.Revoked
        : nowUtc >= token.ExpiresAtUtc ? AccessTokenState.Expired
        : AccessTokenState.Active;

    /// <summary>Last-used is only written when it has moved on by a minute, so a polling client doesn't write on every request.</summary>
    public static bool ShouldRecordUse(DateTime? lastUsedUtc, DateTime nowUtc) =>
        lastUsedUtc is null || nowUtc - lastUsedUtc.Value >= LastUsedResolution;

    public static int NormalizeExpiryDays(int days) =>
        ExpiryChoicesDays.Contains(days) ? days : DefaultExpiryDays;

    private static string Base64UrlEncode(byte[] bytes) =>
        Convert.ToBase64String(bytes).TrimEnd('=').Replace('+', '-').Replace('/', '_');
}
