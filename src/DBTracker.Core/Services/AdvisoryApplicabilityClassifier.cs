using DBTracker.Core.Entities;
using DBTracker.Core.Enums;

namespace DBTracker.Core.Services;

/// <summary>Pure, automatic suggestion for one advisory/instance pairing (D05).</summary>
public record AdvisoryClassificationResult(AdvisoryClassification Classification, string Reasoning);

/// <summary>
/// Automatic initial classification from version-range matching alone (D05). Deliberately never
/// returns Confirmed — a build-in-range match is only ever Potential, because distribution
/// backports and managed-service patching can both mean a version number alone doesn't determine
/// exposure. Only a human reviewer (AdvisoryService.ReviewAssessmentAsync) can set Confirmed or
/// override this to NotApplicable with their own evidence.
/// </summary>
public static class AdvisoryApplicabilityClassifier
{
    public static AdvisoryClassificationResult Classify(AdvisoryRecord advisory, VersionIdentity installed, bool distributionProvidesBackport)
    {
        if (installed.ResolutionStatus != VersionResolutionStatus.Resolved)
        {
            return new AdvisoryClassificationResult(AdvisoryClassification.Unknown,
                "Installed version identity is unresolved — applicability cannot be determined from version alone.");
        }

        if (!string.Equals(advisory.Family, installed.Family, StringComparison.OrdinalIgnoreCase))
        {
            return new AdvisoryClassificationResult(AdvisoryClassification.NotApplicable,
                $"Advisory applies to {advisory.Family}; installed identity is {installed.Family}.");
        }

        var installedBuildKey = BuildKey(installed);
        var inRange = VersionBuildComparer.IsInRange(installedBuildKey, advisory.AffectedMinBuild, advisory.AffectedMaxBuild);

        if (!inRange)
        {
            return new AdvisoryClassificationResult(AdvisoryClassification.NotApplicable,
                $"Installed build {installedBuildKey} falls outside the advisory's affected range ({advisory.AffectedMinBuild}–{advisory.AffectedMaxBuild}).");
        }

        if (!string.IsNullOrWhiteSpace(advisory.FixedBuild) && VersionBuildComparer.Compare(installedBuildKey, advisory.FixedBuild) >= 0)
        {
            return new AdvisoryClassificationResult(AdvisoryClassification.NotApplicable,
                $"Installed build {installedBuildKey} is at or beyond the vendor's fixed build {advisory.FixedBuild}.");
        }

        if (distributionProvidesBackport)
        {
            return new AdvisoryClassificationResult(AdvisoryClassification.Potential,
                "Installed build falls in the affected range, but the distribution may backport the fix independently of upstream version numbers — review required before confirming or dismissing.");
        }

        return new AdvisoryClassificationResult(AdvisoryClassification.Potential,
            "Installed build falls in the advisory's affected range. Version matching alone cannot confirm vulnerability — reviewer evidence is required to confirm or dismiss.");
    }

    private static string BuildKey(VersionIdentity id) =>
        string.IsNullOrWhiteSpace(id.Build) ? $"{id.Major}.{id.Minor}" : $"{id.Major}.{id.Minor}.{id.Build}";
}
