using System.Formats.Cbor;
using DBTracker.Core.Services;
using DBTracker.Infrastructure.Identity;

namespace DBTracker.UnitTests;

/// <summary>G26: the provider hint read from a passkey's attestation object, the Base64URL forms the Signal API needs, and sign-in redirects.</summary>
public class PasskeyHelpersTests
{
    private static readonly Guid GooglePasswordManager = Guid.Parse("ea9b8d66-4d01-1d21-3ce4-b6b48cb575d4");

    private static byte[] AuthData(Guid aaguid, byte flags = 0x45)
    {
        var data = new byte[32 + 1 + 4 + 16 + 2];
        data[32] = flags;
        aaguid.TryWriteBytes(data.AsSpan(37, 16), bigEndian: true, out _);
        return data;
    }

    private static byte[] AttestationObject(byte[] authData, bool authDataFirst = false)
    {
        var writer = new CborWriter();
        writer.WriteStartMap(3);
        if (authDataFirst)
        {
            writer.WriteTextString("authData");
            writer.WriteByteString(authData);
        }
        writer.WriteTextString("fmt");
        writer.WriteTextString("packed");
        writer.WriteTextString("attStmt");
        writer.WriteStartMap(2);
        writer.WriteTextString("alg");
        writer.WriteInt32(-7);
        writer.WriteTextString("x5c");
        writer.WriteStartArray(1);
        writer.WriteByteString([1, 2, 3]);
        writer.WriteEndArray();
        writer.WriteEndMap();
        if (!authDataFirst)
        {
            writer.WriteTextString("authData");
            writer.WriteByteString(authData);
        }
        writer.WriteEndMap();
        return writer.Encode();
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void KnownAaguid_NamesTheProvider_WhereverAuthDataSitsInTheMap(bool authDataFirst)
    {
        var attestation = AttestationObject(AuthData(GooglePasswordManager), authDataFirst);

        Assert.Equal(GooglePasswordManager, PasskeyProviderHint.ReadAaguid(attestation));
        Assert.Equal("Google Password Manager", PasskeyProviderHint.ProviderName(attestation));
    }

    [Fact]
    public void UnknownAaguid_IsReadButHasNoName()
    {
        var aaguid = Guid.Parse("01020304-0506-0708-090a-0b0c0d0e0f10");
        var attestation = AttestationObject(AuthData(aaguid));

        Assert.Equal(aaguid, PasskeyProviderHint.ReadAaguid(attestation));
        Assert.Null(PasskeyProviderHint.ProviderName(attestation));
    }

    [Fact]
    public void ZeroAaguid_MeansNoProviderHint() =>
        Assert.Null(PasskeyProviderHint.ReadAaguid(AttestationObject(AuthData(Guid.Empty))));

    [Fact]
    public void WithoutAttestedCredentialData_ThereIsNoAaguid() =>
        Assert.Null(PasskeyProviderHint.ReadAaguid(AttestationObject(AuthData(GooglePasswordManager, flags: 0x05))));

    [Theory]
    [InlineData(new byte[0])]
    [InlineData(new byte[] { 0xFF, 0x00, 0x13 })]
    [InlineData(new byte[] { 0xA1, 0x68 })]
    public void UnreadableAttestationObjects_GiveNoHint_InsteadOfThrowing(byte[] bytes) =>
        Assert.Null(PasskeyProviderHint.ProviderName(bytes));

    [Fact]
    public void ShortAuthData_GivesNoHint()
    {
        var writer = new CborWriter();
        writer.WriteStartMap(1);
        writer.WriteTextString("authData");
        writer.WriteByteString(new byte[20]);
        writer.WriteEndMap();
        Assert.Null(PasskeyProviderHint.ReadAaguid(writer.Encode()));
    }

    [Fact]
    public void UserHandle_IsTheUserIdAsUnpaddedBase64UrlUtf8()
    {
        Assert.Equal("YWJj", PasskeyService.UserHandle("abc"));
        Assert.DoesNotContain("=", PasskeyService.UserHandle("3f2a9c1e-0000-4b4b-8c8c-000000000001"));
    }

    [Fact]
    public void CredentialIds_RoundTripThroughBase64Url()
    {
        byte[] id = [0xfb, 0xff, 0x00, 0x10, 0x7e];
        var encoded = PasskeyService.EncodeCredentialId(id);

        Assert.Matches("^[A-Za-z0-9_-]+$", encoded);
        Assert.Equal(id, PasskeyService.DecodeCredentialId(encoded));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("not base64url!")]
    public void BadCredentialIds_DecodeToNull(string? value) =>
        Assert.Null(PasskeyService.DecodeCredentialId(value));

    [Fact]
    public void BlankName_FallsBackToTheProvider_ThenToADatedLabel()
    {
        var created = new DateTimeOffset(2026, 11, 11, 9, 0, 0, TimeSpan.Zero);

        Assert.Equal("iCloud Keychain", PasskeyService.NameOrDefault("  ", "iCloud Keychain", created));
        Assert.Equal("Passkey added 11 Nov 2026", PasskeyService.NameOrDefault(null, null, created));
        Assert.Equal("Work laptop", PasskeyService.NameOrDefault("  Work laptop ", "Windows Hello", created));
    }

    [Fact]
    public void LongNames_AreCapped() =>
        Assert.Equal(PasskeyService.MaxNameLength, PasskeyService.NameOrDefault(new string('x', 200), null, DateTimeOffset.UtcNow).Length);

    [Theory]
    [InlineData("/instances?env=Prod", "/instances?env=Prod")]
    [InlineData("/", "/")]
    [InlineData(null, "/")]
    [InlineData("", "/")]
    [InlineData("//evil.example", "/")]
    [InlineData("/\\evil.example", "/")]
    [InlineData("https://evil.example/", "/")]
    [InlineData("instances", "/")]
    [InlineData("/x\r\nLocation: https://evil.example", "/")]
    public void ReturnUrl_OnlyAllowsLocalPaths(string? requested, string expected) =>
        Assert.Equal(expected, ReturnUrlPolicy.LocalOrRoot(requested));
}
