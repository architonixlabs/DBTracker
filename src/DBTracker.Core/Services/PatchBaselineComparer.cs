using DBTracker.Core.Entities;
using DBTracker.Core.Enums;

namespace DBTracker.Core.Services;

/// <summary>Where the installed version sits relative to a catalogued release, compared major, minor, then build.</summary>
public enum BuildPosition
{
    /// <summary>Not enough version detail on one side to compare.</summary>
    NotComparable,
    Behind,
    Same,
    Ahead
}

/// <summary>Four separate facts about an instance's patch position, never collapsed into one "update available" signal.</summary>
/// <param name="RequiredByUtc">Internal policy deadline from the approved baseline. Not a vendor date.</param>
/// <param name="InstalledVsLatestPatch">Build-level comparison with the latest applicable branch patch.</param>
/// <param name="InstalledVsApproved">Build-level comparison with the approved target.</param>
public record PatchBaselineResult(
    string InstalledLabel,
    ReleaseCatalogEntry? LatestApplicableBranchPatch,
    ReleaseCatalogEntry? NewestRelevantGA,
    ReleaseCatalogEntry? ApprovedTarget,
    bool IsMajorUpgradeFromApproved,
    string Notes,
    DateTime? RequiredByUtc = null,
    BuildPosition InstalledVsLatestPatch = BuildPosition.NotComparable,
    BuildPosition InstalledVsApproved = BuildPosition.NotComparable)
{
    /// <summary>The internal deadline has passed and the instance is still behind the approved target (or can't be compared).</summary>
    public bool IsPastRequiredBy(DateTime nowUtc) =>
        ApprovedTarget is not null && RequiredByUtc is { } due && due <= nowUtc &&
        InstalledVsApproved is BuildPosition.Behind or BuildPosition.NotComparable;
}

/// <summary>Patch-position comparison. Previews are excluded by default, and the approved target only comes from a reviewed, unexpired baseline, never from newest GA.</summary>
/// <remarks>A same-branch patch and a major-version approved target are reported separately (IsMajorUpgradeFromApproved), since they are very different actions.
/// The branch patch respects the installed patch train (CU vs GDR, LTS vs Innovation), edition and distribution when the catalogue names them.</remarks>
public static class PatchBaselineComparer
{
    public static PatchBaselineResult Compare(
        VersionIdentity installed,
        IReadOnlyList<ReleaseCatalogEntry> candidatesForFamily,
        ApprovedPatchBaseline? baseline,
        DateTime nowUtc,
        bool includePreview = false)
    {
        var installedLabel = installed.ResolutionStatus == VersionResolutionStatus.Resolved
            ? $"{installed.Family} {installed.Major}{(installed.Minor is { } mn ? $".{mn}" : "")}{(string.IsNullOrEmpty(installed.Build) ? "" : $" build {installed.Build}")}"
            : $"Unresolved ({installed.RawVersionText})";

        var notes = new List<string>();
        var expiredOverrides = candidatesForFamily.Where(c => ReleaseCatalogMatcher.IsOverrideExpired(c, nowUtc)).ToList();
        if (expiredOverrides.Count > 0)
        {
            notes.Add($"{expiredOverrides.Count} catalogue entr{(expiredOverrides.Count == 1 ? "y has" : "ies have")} an expired manual override and {(expiredOverrides.Count == 1 ? "is" : "are")} left out until reviewed: {string.Join(", ", expiredOverrides.Select(c => c.ReleaseLabel))}.");
        }

        var eligible = candidatesForFamily
            .Where(c => includePreview || c.Channel != ReleaseChannel.Preview)
            .Where(c => !ReleaseCatalogMatcher.IsOverrideExpired(c, nowUtc))
            .ToList();

        ReleaseCatalogEntry? latestApplicableBranchPatch = null;
        ReleaseCatalogEntry? newestRelevantGa = null;

        if (installed.ResolutionStatus == VersionResolutionStatus.Resolved)
        {
            latestApplicableBranchPatch = eligible
                .Where(c => c.Major == installed.Major && c.Minor == installed.Minor &&
                            Agrees(c.PatchTrain, installed.PatchTrain) &&
                            Agrees(c.Edition, installed.Edition) &&
                            Agrees(c.Distribution, installed.Distribution))
                .OrderByDescending(c => c, BuildOrderComparer.Instance)
                .FirstOrDefault();

            newestRelevantGa = eligible
                .Where(c => includePreview || c.Channel == ReleaseChannel.GA)
                .Where(c => Agrees(c.Edition, installed.Edition) && Agrees(c.Distribution, installed.Distribution))
                .OrderByDescending(c => c.Major ?? int.MinValue)
                .ThenByDescending(c => c.Minor ?? int.MinValue)
                .ThenByDescending(c => c, BuildOrderComparer.Instance)
                .FirstOrDefault();
        }

        ReleaseCatalogEntry? approvedTarget = null;
        DateTime? requiredBy = null;
        if (baseline is not null)
        {
            if (!baseline.IsActive)
            {
                notes.Add("An approved baseline exists for this family but is inactive.");
            }
            else if (baseline.ExpiresAtUtc is { } exp && exp <= nowUtc)
            {
                notes.Add($"The approved baseline for this family expired on {exp:yyyy-MM-dd} and needs review.");
            }
            else if (baseline.TargetReleaseCatalogEntry is { } target && ReleaseCatalogMatcher.IsOverrideExpired(target, nowUtc))
            {
                notes.Add($"The approved target \"{target.ReleaseLabel}\" has an expired manual override, so it isn't shown as approved until the catalogue entry is reviewed.");
            }
            else
            {
                approvedTarget = baseline.TargetReleaseCatalogEntry;
                requiredBy = baseline.RequiredByUtc;
                if (approvedTarget?.Channel == ReleaseChannel.Preview)
                {
                    notes.Add("The approved target is a preview release. Confirm this was an intentional, reviewed exception.");
                }
            }
        }

        var isMajorUpgrade = approvedTarget is not null &&
            installed.ResolutionStatus == VersionResolutionStatus.Resolved &&
            approvedTarget.Major != installed.Major;

        return new PatchBaselineResult(installedLabel, latestApplicableBranchPatch, newestRelevantGa, approvedTarget, isMajorUpgrade,
            string.Join(" ", notes), requiredBy,
            ComparePosition(installed, latestApplicableBranchPatch),
            ComparePosition(installed, approvedTarget));
    }

    /// <summary>Compares major, then minor, then build. Missing pieces make it NotComparable rather than a guess.</summary>
    public static BuildPosition ComparePosition(VersionIdentity installed, ReleaseCatalogEntry? release)
    {
        if (release is null || installed.ResolutionStatus != VersionResolutionStatus.Resolved || installed.Major is null || release.Major is null)
        {
            return BuildPosition.NotComparable;
        }

        if (installed.Major != release.Major)
        {
            return installed.Major < release.Major ? BuildPosition.Behind : BuildPosition.Ahead;
        }

        if (installed.Minor is { } im && release.Minor is { } rm && im != rm)
        {
            return im < rm ? BuildPosition.Behind : BuildPosition.Ahead;
        }

        if (string.IsNullOrWhiteSpace(installed.Build) || string.IsNullOrWhiteSpace(release.Build))
        {
            return BuildPosition.NotComparable;
        }

        var cmp = VersionBuildComparer.Compare(installed.Build, release.Build);
        return cmp < 0 ? BuildPosition.Behind : cmp > 0 ? BuildPosition.Ahead : BuildPosition.Same;
    }

    private static bool Agrees(string entryValue, string identityValue) =>
        string.IsNullOrWhiteSpace(entryValue) ||
        string.Equals(entryValue.Trim(), (identityValue ?? "").Trim(), StringComparison.OrdinalIgnoreCase);

    private sealed class BuildOrderComparer : IComparer<ReleaseCatalogEntry>
    {
        public static readonly BuildOrderComparer Instance = new();
        public int Compare(ReleaseCatalogEntry? x, ReleaseCatalogEntry? y) => VersionBuildComparer.Compare(x?.Build, y?.Build);
    }
}
