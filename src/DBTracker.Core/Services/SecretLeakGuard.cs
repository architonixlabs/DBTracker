namespace DBTracker.Core.Services;

/// <summary>Screens free-text certificate/key fields for pasted secrets before they reach the UI, database or exports. A backstop, since no field is meant to hold one.</summary>
public static class SecretLeakGuard
{
    private static readonly string[] SecretMarkers =
    [
        "-----BEGIN", "PRIVATE KEY", "BEGIN RSA", "BEGIN EC PRIVATE", "BEGIN OPENSSH"
    ];

    private static readonly string[] AssignmentMarkers =
    [
        "password=", "password:", "pwd=", "pwd:", "secret=", "secret:", "apikey=", "api_key=", "api-key="
    ];

    public static bool LooksLikeSecretMaterial(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return false;
        }

        return SecretMarkers.Any(m => value.Contains(m, StringComparison.OrdinalIgnoreCase))
            || AssignmentMarkers.Any(m => value.Contains(m, StringComparison.OrdinalIgnoreCase));
    }

    /// <summary>Returns the value unchanged unless it looks like secret material, which is replaced whole with a redaction marker.</summary>
    public static string Redact(string? value) => LooksLikeSecretMaterial(value) ? "[redacted — looked like secret/private-key material]" : value ?? string.Empty;
}
