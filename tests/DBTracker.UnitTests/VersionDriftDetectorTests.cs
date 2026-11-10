using DBTracker.Core.Entities;
using DBTracker.Core.Enums;
using DBTracker.Core.Services;
using Xunit;

namespace DBTracker.UnitTests;

public class VersionDriftDetectorTests
{
    private static VersionIdentity Declared(string raw = "16.2", int major = 16, int minor = 2, string build = "") => new()
    {
        RawVersionText = raw,
        ResolutionStatus = VersionResolutionStatus.Resolved,
        Family = "PostgreSQL",
        Major = major,
        Minor = minor,
        Build = build
    };

    [Fact]
    public void FailedObservation_IsAlwaysUnknown_NeverDrift()
    {
        var observation = new VersionObservation { Outcome = ObservationOutcome.PermissionDenied, RawVersionText = "99.9" };
        var result = VersionDriftDetector.Detect(Declared(), observation, DatabaseEngine.PostgreSql);

        Assert.Equal(DriftStatus.Unknown, result.Status);
        Assert.Empty(result.DifferingFields);
    }

    [Fact]
    public void SameRawText_IsMatch()
    {
        var observation = new VersionObservation { Outcome = ObservationOutcome.Success, RawVersionText = "16.2" };
        var result = VersionDriftDetector.Detect(Declared(), observation, DatabaseEngine.PostgreSql);

        Assert.Equal(DriftStatus.Match, result.Status);
    }

    [Fact]
    public void DifferentRawText_SameNormalizedVersion_IsMatch()
    {
        // Case-insensitivity / incidental text difference shouldn't false-positive as drift once normalized.
        var observation = new VersionObservation { Outcome = ObservationOutcome.Success, RawVersionText = "16.2 " };
        var result = VersionDriftDetector.Detect(Declared(raw: "16.2"), observation, DatabaseEngine.PostgreSql);

        Assert.Equal(DriftStatus.Match, result.Status);
    }

    [Fact]
    public void DifferentMinorVersion_IsDrift()
    {
        var observation = new VersionObservation { Outcome = ObservationOutcome.Success, RawVersionText = "16.4" };
        var result = VersionDriftDetector.Detect(Declared(raw: "16.2", minor: 2), observation, DatabaseEngine.PostgreSql);

        Assert.Equal(DriftStatus.Drift, result.Status);
        Assert.Contains("Minor", result.DifferingFields);
    }

    [Fact]
    public void ObservedUnparseable_StillFlagsRawTextDrift_WithoutStructuredFieldDiffs()
    {
        var observation = new VersionObservation { Outcome = ObservationOutcome.Success, RawVersionText = "garbled" };
        var result = VersionDriftDetector.Detect(Declared(), observation, DatabaseEngine.PostgreSql);

        Assert.Equal(DriftStatus.Drift, result.Status);
        Assert.Contains("RawVersionText", result.DifferingFields);
        Assert.DoesNotContain("Major", result.DifferingFields);
    }
}
