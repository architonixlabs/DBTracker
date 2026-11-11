using System.Formats.Cbor;

namespace DBTracker.Infrastructure.Identity;

/// <summary>G26: names the passkey provider (Google Password Manager, iCloud Keychain, ...) from the AAGUID in a passkey's stored attestation object.</summary>
/// <remarks>
/// The AAGUID is a UX hint only. Platform passkeys aren't attested, so a browser can report any value: never use it for an access decision.
/// The table is a small, vendored subset of the community list at github.com/passkeydeveloper/passkey-authenticator-aaguids, kept here
/// instead of fetched because the app makes no third-party requests.
/// </remarks>
public static class PasskeyProviderHint
{
    private static readonly Dictionary<Guid, string> KnownProviders = new()
    {
        [Guid.Parse("ea9b8d66-4d01-1d21-3ce4-b6b48cb575d4")] = "Google Password Manager",
        [Guid.Parse("adce0002-35bc-c60a-648b-0b25f1f05503")] = "Chrome on Mac",
        [Guid.Parse("fbfc3007-154e-4ecc-8c0b-6e020557d7bd")] = "iCloud Keychain",
        [Guid.Parse("dd4ec289-e01d-41c9-bb89-70fa845d4bf2")] = "iCloud Keychain (managed)",
        [Guid.Parse("08987058-cadc-4b81-b6e1-30de50dcbe96")] = "Windows Hello",
        [Guid.Parse("9ddd1817-af5a-4672-a2b9-3e3dd95000a9")] = "Windows Hello",
        [Guid.Parse("6028b017-b1d4-4c02-b4b3-afcdafc96bb2")] = "Windows Hello",
        [Guid.Parse("bada5566-a7aa-401f-bd96-45619a55120d")] = "1Password",
        [Guid.Parse("d548826e-79b4-db40-a3d8-11116f7e8349")] = "Bitwarden",
        [Guid.Parse("531126d6-e717-415c-9320-3d9aa6981239")] = "Dashlane",
        [Guid.Parse("53414d53-554e-4700-0000-000000000000")] = "Samsung Pass",
    };

    // authenticatorData: rpIdHash (32) | flags (1) | signCount (4) | AAGUID (16) | ... (WebAuthn 6.1).
    private const int AaguidOffset = 37;
    private const byte AttestedCredentialDataFlag = 0x40;

    /// <summary>The AAGUID in the attestation object, or null when it's missing, all zeros, or the object can't be read.</summary>
    public static Guid? ReadAaguid(byte[]? attestationObject)
    {
        if (attestationObject is not { Length: > 0 })
        {
            return null;
        }

        try
        {
            var reader = new CborReader(attestationObject, CborConformanceMode.Lax);
            var count = reader.ReadStartMap();
            for (var i = 0; count is null || i < count; i++)
            {
                if (count is null && reader.PeekState() == CborReaderState.EndMap)
                {
                    break;
                }
                var key = reader.PeekState() == CborReaderState.TextString ? reader.ReadTextString() : null;
                if (key == "authData")
                {
                    return FromAuthenticatorData(reader.ReadByteString());
                }
                if (key is null)
                {
                    reader.SkipValue();
                }
                reader.SkipValue();
            }
        }
        catch (CborContentException)
        {
        }
        catch (InvalidOperationException)
        {
        }
        return null;
    }

    private static Guid? FromAuthenticatorData(byte[] authData)
    {
        if (authData.Length < AaguidOffset + 16 || (authData[32] & AttestedCredentialDataFlag) == 0)
        {
            return null;
        }
        var aaguid = new Guid(authData.AsSpan(AaguidOffset, 16), bigEndian: true);
        return aaguid == Guid.Empty ? null : aaguid;
    }

    /// <summary>A provider name for the passkey list, or null when the provider isn't known.</summary>
    public static string? ProviderName(byte[]? attestationObject) =>
        ReadAaguid(attestationObject) is { } aaguid && KnownProviders.TryGetValue(aaguid, out var name) ? name : null;
}
