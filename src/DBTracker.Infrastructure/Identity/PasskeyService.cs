using System.Buffers.Text;
using System.Globalization;
using System.Text;
using DBTracker.Infrastructure.Audit;
using Microsoft.AspNetCore.Identity;

namespace DBTracker.Infrastructure.Identity;

/// <summary>One row on the Passkeys page. CredentialId is unpadded Base64URL, the form the browser's Signal API expects.</summary>
public record PasskeySummary(string CredentialId, string Name, DateTimeOffset CreatedAt, string? Provider, bool IsBackedUp);

public record PasskeyChangeResult(bool Succeeded, string? Error = null);

/// <summary>G26: list, add, rename and remove a user's passkeys through Identity's passkey store. Adding, renaming and removing are audited.</summary>
/// <remarks>Passkeys live only in AspNetUserPasskeys. No export reads that table, and nothing here returns key material.</remarks>
public class PasskeyService(UserManager<ApplicationUser> userManager, AuditRecorder audit)
{
    public const int MaxNameLength = 64;

    /// <summary>The WebAuthn user handle: Identity encodes the user's Id as UTF-8 bytes, so the Signal API gets the same value as Base64URL.</summary>
    public static string UserHandle(string userId) => Base64Url.EncodeToString(Encoding.UTF8.GetBytes(userId));

    public static string EncodeCredentialId(byte[] credentialId) => Base64Url.EncodeToString(credentialId);

    public static byte[]? DecodeCredentialId(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return null;
        }
        try
        {
            return Base64Url.DecodeFromChars(value.Trim());
        }
        catch (FormatException)
        {
            return null;
        }
    }

    /// <summary>Trims, caps the length, and falls back to the provider name or a dated label so a row is never blank.</summary>
    public static string NameOrDefault(string? requested, string? provider, DateTimeOffset createdAt)
    {
        var name = (requested ?? "").Trim();
        if (name.Length == 0)
        {
            name = provider ?? string.Create(CultureInfo.InvariantCulture, $"Passkey added {createdAt.UtcDateTime:d MMM yyyy}");
        }
        return name.Length > MaxNameLength ? name[..MaxNameLength] : name;
    }

    public async Task<IReadOnlyList<PasskeySummary>> ListAsync(ApplicationUser user)
    {
        var passkeys = await userManager.GetPasskeysAsync(user);
        return passkeys
            .OrderByDescending(p => p.CreatedAt)
            .Select(p =>
            {
                var provider = PasskeyProviderHint.ProviderName(p.AttestationObject);
                return new PasskeySummary(EncodeCredentialId(p.CredentialId), NameOrDefault(p.Name, provider, p.CreatedAt), p.CreatedAt, provider, p.IsBackedUp);
            })
            .ToList();
    }

    /// <summary>Stores a passkey that Identity has just verified (PerformPasskeyAttestationAsync) and records it in the audit trail.</summary>
    public async Task<PasskeyChangeResult> AddAsync(ApplicationUser user, UserPasskeyInfo passkey, string? requestedName)
    {
        var provider = PasskeyProviderHint.ProviderName(passkey.AttestationObject);
        passkey.Name = NameOrDefault(requestedName, provider, passkey.CreatedAt);

        var result = await userManager.AddOrUpdatePasskeyAsync(user, passkey);
        if (!result.Succeeded)
        {
            return new PasskeyChangeResult(false, "The passkey couldn't be saved. Try again.");
        }

        await audit.RecordUserActionAsync(user.Id, ActorName(user), "PasskeyAdded", "User", user.Id, ActorName(user),
            $"Added passkey \"{passkey.Name}\"{(provider is null ? "" : $" ({provider})")}.");
        return new PasskeyChangeResult(true);
    }

    public async Task<PasskeyChangeResult> RenameAsync(ApplicationUser user, string credentialId, string newName)
    {
        var name = (newName ?? "").Trim();
        if (name.Length == 0)
        {
            return new PasskeyChangeResult(false, "Enter a name for the passkey.");
        }
        if (name.Length > MaxNameLength)
        {
            return new PasskeyChangeResult(false, $"Keep the name to {MaxNameLength} characters or fewer.");
        }

        var passkey = DecodeCredentialId(credentialId) is { } id ? await userManager.GetPasskeyAsync(user, id) : null;
        if (passkey is null)
        {
            return new PasskeyChangeResult(false, "That passkey was already removed.");
        }

        var oldName = passkey.Name ?? "";
        passkey.Name = name;
        var result = await userManager.AddOrUpdatePasskeyAsync(user, passkey);
        if (!result.Succeeded)
        {
            return new PasskeyChangeResult(false, "The passkey couldn't be renamed. Try again.");
        }

        await audit.RecordUserActionAsync(user.Id, ActorName(user), "PasskeyRenamed", "User", user.Id, ActorName(user),
            $"Renamed passkey \"{oldName}\" to \"{name}\".");
        return new PasskeyChangeResult(true);
    }

    public async Task<PasskeyChangeResult> RemoveAsync(ApplicationUser user, string credentialId)
    {
        var id = DecodeCredentialId(credentialId);
        var passkey = id is null ? null : await userManager.GetPasskeyAsync(user, id);
        if (passkey is null)
        {
            return new PasskeyChangeResult(false, "That passkey was already removed.");
        }

        var result = await userManager.RemovePasskeyAsync(user, id!);
        if (!result.Succeeded)
        {
            return new PasskeyChangeResult(false, "The passkey couldn't be removed. Try again.");
        }

        var provider = PasskeyProviderHint.ProviderName(passkey.AttestationObject);
        await audit.RecordUserActionAsync(user.Id, ActorName(user), "PasskeyRemoved", "User", user.Id, ActorName(user),
            $"Removed passkey \"{NameOrDefault(passkey.Name, provider, passkey.CreatedAt)}\".");
        return new PasskeyChangeResult(true);
    }

    private static string ActorName(ApplicationUser user) => user.Email ?? user.UserName ?? user.Id;
}
