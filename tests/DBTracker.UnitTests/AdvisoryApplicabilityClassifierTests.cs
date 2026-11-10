using DBTracker.Core.Entities;
using DBTracker.Core.Enums;
using DBTracker.Core.Services;
using Xunit;

namespace DBTracker.UnitTests;

public class AdvisoryApplicabilityClassifierTests
{
    private static VersionIdentity Installed(int major = 16, int minor = 2, VersionResolutionStatus status = VersionResolutionStatus.Resolved) => new()
    {
        ResolutionStatus = status,
        Family = "PostgreSQL",
        Major = major,
        Minor = minor,
        Build = ""
    };

    private static AdvisoryRecord Advisory(string family = "PostgreSQL", string min = "16.0", string max = "16.3", string fixedBuild = "") => new()
    {
        Family = family,
        AffectedMinBuild = min,
        AffectedMaxBuild = max,
        FixedBuild = fixedBuild
    };

    [Fact]
    public void UnresolvedInstalled_IsUnknown()
    {
        var result = AdvisoryApplicabilityClassifier.Classify(Advisory(), Installed(status: VersionResolutionStatus.Unresolved), false);
        Assert.Equal(AdvisoryClassification.Unknown, result.Classification);
    }

    [Fact]
    public void DifferentFamily_IsNotApplicable()
    {
        var result = AdvisoryApplicabilityClassifier.Classify(Advisory(family: "MySQL"), Installed(), false);
        Assert.Equal(AdvisoryClassification.NotApplicable, result.Classification);
    }

    [Fact]
    public void OutsideAffectedRange_IsNotApplicable()
    {
        var result = AdvisoryApplicabilityClassifier.Classify(Advisory(min: "17.0", max: "17.5"), Installed(major: 16, minor: 2), false);
        Assert.Equal(AdvisoryClassification.NotApplicable, result.Classification);
    }

    [Fact]
    public void WithinRange_IsPotential_NeverConfirmedAutomatically()
    {
        var result = AdvisoryApplicabilityClassifier.Classify(Advisory(), Installed(major: 16, minor: 2), false);
        Assert.Equal(AdvisoryClassification.Potential, result.Classification);
    }

    [Fact]
    public void AtOrBeyondFixedBuild_IsNotApplicable()
    {
        var result = AdvisoryApplicabilityClassifier.Classify(Advisory(min: "16.0", max: "16.5", fixedBuild: "16.2"), Installed(major: 16, minor: 2), false);
        Assert.Equal(AdvisoryClassification.NotApplicable, result.Classification);
    }

    [Fact]
    public void DistributionBackport_StillPotential_ButNotesBackportCaveat()
    {
        var result = AdvisoryApplicabilityClassifier.Classify(Advisory(), Installed(major: 16, minor: 2), true);
        Assert.Equal(AdvisoryClassification.Potential, result.Classification);
        Assert.Contains("backport", result.Reasoning, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Reasoning_IsNeverEmpty()
    {
        var result = AdvisoryApplicabilityClassifier.Classify(Advisory(), Installed(), false);
        Assert.NotEmpty(result.Reasoning);
    }
}
