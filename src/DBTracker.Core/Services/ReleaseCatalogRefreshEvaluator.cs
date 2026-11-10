using DBTracker.Core.Entities;

namespace DBTracker.Core.Services;

/// <summary>Pure result of attempting to refresh one catalogue entry (D02).</summary>
public record ReleaseCatalogRefreshOutcome(bool Succeeded, string Error);

/// <summary>
/// Whether a catalogue-entry "refresh" attempt can succeed, and why not when it can't. This app
/// deliberately does not call any live vendor website (per the review: "a unit test must not
/// depend on today's live vendor website", and the delivery recommendation to curate facts
/// manually before building live feeds) — a refresh only re-confirms a source that's actually
/// configured. On failure, the caller (ReleaseCatalogService) must retain every existing field
/// unchanged and only record the staleness warning.
/// </summary>
public static class ReleaseCatalogRefreshEvaluator
{
    public static ReleaseCatalogRefreshOutcome Evaluate(ReleaseCatalogEntry entry)
    {
        if (string.IsNullOrWhiteSpace(entry.SourceUrl))
        {
            return new ReleaseCatalogRefreshOutcome(false, "No source URL configured for this entry — nothing to refresh against.");
        }

        if (entry.OverrideExpiresUtc is { } expires && expires <= DateTime.UtcNow)
        {
            return new ReleaseCatalogRefreshOutcome(false, $"The manual override on this entry expired on {expires:yyyy-MM-dd} and needs re-review before the entry can be treated as current.");
        }

        return new ReleaseCatalogRefreshOutcome(true, "");
    }
}
