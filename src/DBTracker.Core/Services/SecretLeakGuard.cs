namespace DBTracker.Core.Services;

/// <summary>
/// D20 — a defensive screen applied to every free-text field that reaches the UI, the database, or a
/// CSV/report export around certificate/recovery-key dependencies, even though no field on
/// CertificateDependency is meant to ever hold a secret/private key in the first place. Belt and
/// suspenders: if a password or private-key value is ever pasted into a notes/evidence field by
/// mistake, this is what keeps it from silently surfacing in an export.
/// </summary>
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

    /// <summary>Returns the value unchanged unless it looks like secret material, in which case it's replaced with a fixed redaction marker — never partially shown.</summary>
    public static string Redact(string? value) => LooksLikeSecretMaterial(value) ? "[redacted — looked like secret/private-key material]" : value ?? string.Empty;
}
