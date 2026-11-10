using DBTracker.Core.Services;
using Xunit;

namespace DBTracker.UnitTests;

public class SecretLeakGuardTests
{
    [Theory]
    [InlineData("-----BEGIN PRIVATE KEY-----\nMIIEvQ...\n-----END PRIVATE KEY-----")]
    [InlineData("-----BEGIN RSA PRIVATE KEY-----")]
    [InlineData("password=Sup3rSecret!")]
    [InlineData("pwd: hunter2")]
    [InlineData("api_key=sk-abcdef1234567890")]
    public void LooksLikeSecretMaterial_FlagsKnownPatterns(string value)
    {
        Assert.True(SecretLeakGuard.LooksLikeSecretMaterial(value));
    }

    [Theory]
    [InlineData("Vault: kv/sql-prod-tde-key")]
    [InlineData("Renewed via internal CA ticket #4821")]
    [InlineData("")]
    [InlineData(null)]
    public void OrdinaryReferenceText_IsNotFlagged(string? value)
    {
        Assert.False(SecretLeakGuard.LooksLikeSecretMaterial(value));
    }

    [Fact]
    public void Redact_ReplacesFlaggedValue_WithFixedMarker_NeverPartialSecret()
    {
        var redacted = SecretLeakGuard.Redact("password=Sup3rSecret!");

        Assert.DoesNotContain("Sup3rSecret", redacted);
        Assert.Equal("[redacted — looked like secret/private-key material]", redacted);
    }

    [Fact]
    public void Redact_LeavesOrdinaryTextUnchanged()
    {
        Assert.Equal("Vault: kv/sql-prod-tde-key", SecretLeakGuard.Redact("Vault: kv/sql-prod-tde-key"));
    }
}
