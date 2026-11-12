namespace DBTracker.Core.Services;

/// <summary>While an installed DBTracker can't reach its database yet (for example SQL Server still starting after a
/// reboot), pages answer 503 with a plain explanation. These paths stay open: health for the tray and monitoring, and the
/// static files the explanation page and a later reload need.</summary>
public static class DatabaseReadinessGate
{
    public const int RetryAfterSeconds = 15;

    private static readonly string[] OpenPrefixes = ["/healthz", "/_framework", "/_content", "/css", "/js", "/lib", "/fonts", "/favicon"];

    public static bool Allows(string? path)
    {
        if (string.IsNullOrEmpty(path))
        {
            return false;
        }

        return OpenPrefixes.Any(p => path.Equals(p, StringComparison.OrdinalIgnoreCase)
            || path.StartsWith(p + "/", StringComparison.OrdinalIgnoreCase)
            || path.StartsWith(p + ".", StringComparison.OrdinalIgnoreCase))
            || path.EndsWith(".css", StringComparison.OrdinalIgnoreCase)
            || path.EndsWith(".js", StringComparison.OrdinalIgnoreCase)
            || path.EndsWith(".woff2", StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>Attempt n (1-based) waits this long before the next try during start-up: 2, 4, 8 seconds.</summary>
    public static TimeSpan StartupBackoff(int attempt) => TimeSpan.FromSeconds(Math.Pow(2, Math.Clamp(attempt, 1, 3)));
}
