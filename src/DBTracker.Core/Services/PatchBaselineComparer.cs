using DBTracker.Core.Entities;
using DBTracker.Core.Enums;

namespace DBTracker.Core.Services;

/// <summary>Four distinct facts about one instance's patch position (D04) — never collapsed into a single "update available" signal.</summary>
public record PatchBaselineResult(
    string InstalledLabel,
    ReleaseCatalogEntry? LatestApplicableBranchPatch,
    ReleaseCatalogEntry? NewestRelevantGA,
    ReleaseCatalogEntry? ApprovedTarget,
    bool IsMajorUpgradeFromApproved,
    string Notes);

/// <summary>
/// Pure comparison logic for D04. Previews are excluded by default (includePreview). The approved
/// target only ever comes from an explicit, reviewed, non-expired ApprovedPatchBaseline — this
/// method never promotes "newest GA" into "approved" on its own, so an unsupported/unreviewed
/// target is never auto-recommended. A same-branch patch and a different-major "approved" target
/// are reported as separate facts (IsMajorUpgradeFromApproved) rather than one blended
/// recommendation, because a patch within a supported branch is a materially different action from
/// a major-version migration.
/// </summary>
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

        var eligible = candidatesForFamily
            .Where(c => includePreview || c.Channel != ReleaseChannel.Preview)
            .ToList();

        ReleaseCatalogEntry? latestApplicableBranchPatch = null;
        ReleaseCatalogEntry? newestRelevantGa = null;

        if (installed.ResolutionStatus == VersionResolutionStatus.Resolved)
        {
            latestApplicableBranchPatch = eligible
                .Where(c => c.Major == installed.Major && c.Minor == installed.Minor)
                .OrderByDescending(c => c, BuildOrderComparer.Instance)
                .FirstOrDefault();

            newestRelevantGa = eligible
                .Where(c => includePreview || c.Channel == ReleaseChannel.GA)
                .OrderByDescending(c => c.Major ?? int.MinValue)
                .ThenByDescending(c => c.Minor ?? int.MinValue)
                .ThenByDescending(c => c, BuildOrderComparer.Instance)
                .FirstOrDefault();
        }

        ReleaseCatalogEntry? approvedTarget = null;
        var notes = "";
        if (baseline is not null)
        {
            if (!baseline.IsActive)
            {
                notes = "An approved baseline exists for this family but is inactive.";
            }
            else if (baseline.ExpiresAtUtc is { } exp && exp <= nowUtc)
            {
                notes = $"The approved baseline for this family expired on {exp:yyyy-MM-dd} and needs review.";
            }
            else
            {
                approvedTarget = baseline.TargetReleaseCatalogEntry;
                if (approvedTarget?.Channel == ReleaseChannel.Preview)
                {
                    notes = "The approved target is a preview release — confirm this was an intentional, reviewed exception.";
                }
            }
        }

        var isMajorUpgrade = approvedTarget is not null &&
            installed.ResolutionStatus == VersionResolutionStatus.Resolved &&
            approvedTarget.Major != installed.Major;

        return new PatchBaselineResult(installedLabel, latestApplicableBranchPatch, newestRelevantGa, approvedTarget, isMajorUpgrade, notes);
    }

    private sealed class BuildOrderComparer : IComparer<ReleaseCatalogEntry>
    {
        public static readonly BuildOrderComparer Instance = new();
        public int Compare(ReleaseCatalogEntry? x, ReleaseCatalogEntry? y) => VersionBuildComparer.Compare(x?.Build, y?.Build);
    }
}
