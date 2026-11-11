using DBTracker.Core.Entities;
using DBTracker.Core.Enums;

namespace DBTracker.Core.Services;

/// <summary>Picks the catalogue entry whose lifecycle facts apply to one resolved identity. Edition, distribution and
/// patch train (the release channel, e.g. LTS vs Innovation or CU vs GDR) must agree when the entry names them; an entry
/// that names an edition the identity doesn't record can't be confirmed, so it isn't used. Previews never match.</summary>
public static class ReleaseCatalogMatcher
{
    public static ReleaseCatalogEntry? Match(IEnumerable<ReleaseCatalogEntry> entries, DatabaseEngine engine, VersionIdentity? identity)
    {
        if (identity is not { ResolutionStatus: VersionResolutionStatus.Resolved } || identity.Major is null)
        {
            return null;
        }

        var branchUsesMinor = BranchUsesMinor(engine, identity.Major.Value);

        return entries
            .Where(c => c.Engine == engine &&
                        string.Equals(c.Family.Trim(), identity.Family.Trim(), StringComparison.OrdinalIgnoreCase) &&
                        c.Major == identity.Major &&
                        c.Channel != ReleaseChannel.Preview &&
                        (!branchUsesMinor || c.Minor is null || identity.Minor is null || c.Minor == identity.Minor) &&
                        Agrees(c.Edition, identity.Edition) &&
                        Agrees(c.Distribution, identity.Distribution) &&
                        Agrees(c.PatchTrain, identity.PatchTrain))
            .OrderByDescending(c => Specificity(c))
            .ThenByDescending(c => HasAnyMilestone(c))
            .ThenByDescending(c => !string.IsNullOrWhiteSpace(c.Build) && string.Equals(c.Build.Trim(), identity.Build.Trim(), StringComparison.OrdinalIgnoreCase))
            .ThenByDescending(c => c.Minor is not null && c.Minor == identity.Minor)
            .ThenByDescending(c => c.Channel != ReleaseChannel.Patch)
            .ThenByDescending(c => c.Minor ?? int.MinValue)
            .ThenByDescending(c => c.ReviewedAtUtc ?? DateTime.MinValue)
            .FirstOrDefault();
    }

    /// <summary>PostgreSQL 10 and later supports a whole major version, and "16.2" is a patch level, so the minor number
    /// doesn't pick a different branch. For the other engines (MySQL 8.0 vs 8.4, for example) it does.</summary>
    public static bool BranchUsesMinor(DatabaseEngine engine, int major) =>
        !(engine == DatabaseEngine.PostgreSql && major >= 10);

    /// <summary>True when a manual override has passed its expiry. Its facts then need review and are not used for runway
    /// or baselines.</summary>
    public static bool IsOverrideExpired(ReleaseCatalogEntry entry, DateTime nowUtc) =>
        entry.IsManualOverride && entry.OverrideExpiresUtc is { } expires && expires <= nowUtc;

    private static bool Agrees(string entryValue, string identityValue) =>
        string.IsNullOrWhiteSpace(entryValue) ||
        string.Equals(entryValue.Trim(), (identityValue ?? "").Trim(), StringComparison.OrdinalIgnoreCase);

    private static int Specificity(ReleaseCatalogEntry c) =>
        new[] { c.Edition, c.Distribution, c.PatchTrain }.Count(s => !string.IsNullOrWhiteSpace(s));

    private static bool HasAnyMilestone(ReleaseCatalogEntry c) =>
        c.MainstreamSupportEndUtc is not null || c.ExtendedSupportEndUtc is not null || c.EsuSupportEndUtc is not null || c.DatesAreTbd || c.DatesConflict;
}
