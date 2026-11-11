using DBTracker.Core.Entities;

namespace DBTracker.Core.Services;

/// <summary>Result of attempting to refresh one catalogue entry.</summary>
public record ReleaseCatalogRefreshOutcome(bool Succeeded, string Error);

/// <summary>Whether a catalogue refresh can succeed, and why not. No live vendor site is called; on failure the caller keeps every field and records only the staleness warning.</summary>
public static class ReleaseCatalogRefreshEvaluator
{
    public static ReleaseCatalogRefreshOutcome Evaluate(ReleaseCatalogEntry entry)
    {
        if (string.IsNullOrWhiteSpace(entry.SourceUrl))
        {
            return new ReleaseCatalogRefreshOutcome(false, "No source URL configured for this entry, so there's nothing to refresh against.");
        }

        if (entry.OverrideExpiresUtc is { } expires && expires <= DateTime.UtcNow)
        {
            return new ReleaseCatalogRefreshOutcome(false, $"The manual override on this entry expired on {expires:yyyy-MM-dd} and needs re-review before the entry can be treated as current.");
        }

        return new ReleaseCatalogRefreshOutcome(true, "");
    }
}
