using DBTracker.Core.Entities;
using DBTracker.Core.Services;
using Xunit;

namespace DBTracker.UnitTests;

public class PersonalAccessTokenCodecTests
{
    private static readonly DateTime Now = new(2026, 11, 11, 9, 0, 0, DateTimeKind.Utc);

    [Fact]
    public void Generate_ProducesPrefixedBase64UrlTokenOfFixedLength()
    {
        var generated = PersonalAccessTokenCodec.Generate();

        Assert.StartsWith("dbt_", generated.Token);
        Assert.Equal(4 + 43, generated.Token.Length);
        Assert.True(PersonalAccessTokenCodec.IsWellFormed(generated.Token));
        Assert.DoesNotContain('=', generated.Token);
        Assert.DoesNotContain('+', generated.Token);
        Assert.DoesNotContain('/', generated.Token);
    }

    [Fact]
    public void Generate_IsRandomEachTime()
    {
        var tokens = Enumerable.Range(0, 50).Select(_ => PersonalAccessTokenCodec.Generate().Token).ToHashSet();
        Assert.Equal(50, tokens.Count);
    }

    [Fact]
    public void Generate_StoresOnlyTheHashAndAShortPrefix()
    {
        var generated = PersonalAccessTokenCodec.Generate();

        Assert.Equal(PersonalAccessTokenCodec.Hash(generated.Token), generated.Hash);
        Assert.Equal(64, generated.Hash.Length);
        Assert.Matches("^[0-9a-f]{64}$", generated.Hash);
        Assert.Equal(generated.Token[..10], generated.DisplayPrefix);
        Assert.DoesNotContain(generated.Token, generated.Hash);
    }

    [Fact]
    public void Hash_IsDeterministic()
    {
        Assert.Equal(PersonalAccessTokenCodec.Hash("dbt_abc"), PersonalAccessTokenCodec.Hash("dbt_abc"));
        Assert.NotEqual(PersonalAccessTokenCodec.Hash("dbt_abc"), PersonalAccessTokenCodec.Hash("dbt_abd"));
    }

    [Fact]
    public void Matches_TrueOnlyForTheSameToken()
    {
        var generated = PersonalAccessTokenCodec.Generate();
        var other = PersonalAccessTokenCodec.Generate();

        Assert.True(PersonalAccessTokenCodec.Matches(generated.Token, generated.Hash));
        Assert.False(PersonalAccessTokenCodec.Matches(other.Token, generated.Hash));
        Assert.False(PersonalAccessTokenCodec.Matches(generated.Token, ""));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("dbt_")]
    [InlineData("abc_AAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAA")]
    [InlineData("dbt_AAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAA")]
    [InlineData("dbt_AAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAA=")]
    [InlineData("dbt_AAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAA/")]
    [InlineData("dbt_AAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAA")]
    public void IsWellFormed_RejectsMalformedTokens(string? token)
    {
        Assert.False(PersonalAccessTokenCodec.IsWellFormed(token));
    }

    [Fact]
    public void StateOf_RevokedWinsOverExpiry()
    {
        var token = new PersonalAccessToken { ExpiresAtUtc = Now.AddDays(-1), RevokedAtUtc = Now.AddDays(-2) };
        Assert.Equal(AccessTokenState.Revoked, PersonalAccessTokenCodec.StateOf(token, Now));
    }

    [Fact]
    public void StateOf_ExpiresAtTheExpiryInstant()
    {
        var token = new PersonalAccessToken { ExpiresAtUtc = Now };
        Assert.Equal(AccessTokenState.Expired, PersonalAccessTokenCodec.StateOf(token, Now));
        Assert.Equal(AccessTokenState.Active, PersonalAccessTokenCodec.StateOf(token, Now.AddSeconds(-1)));
    }

    [Fact]
    public void ShouldRecordUse_AtMostOncePerMinute()
    {
        Assert.True(PersonalAccessTokenCodec.ShouldRecordUse(null, Now));
        Assert.False(PersonalAccessTokenCodec.ShouldRecordUse(Now.AddSeconds(-59), Now));
        Assert.True(PersonalAccessTokenCodec.ShouldRecordUse(Now.AddMinutes(-1), Now));
    }

    [Theory]
    [InlineData(30, 30)]
    [InlineData(90, 90)]
    [InlineData(180, 180)]
    [InlineData(365, 365)]
    [InlineData(0, 90)]
    [InlineData(10000, 90)]
    public void NormalizeExpiryDays_FallsBackToTheDefault(int requested, int expected)
    {
        Assert.Equal(expected, PersonalAccessTokenCodec.NormalizeExpiryDays(requested));
    }
}
