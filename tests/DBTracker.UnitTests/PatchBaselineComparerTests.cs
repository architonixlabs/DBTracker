using DBTracker.Core.Entities;
using DBTracker.Core.Enums;
using DBTracker.Core.Services;
using Xunit;

namespace DBTracker.UnitTests;

public class PatchBaselineComparerTests
{
    private static readonly DateTime Now = new(2026, 11, 10, 0, 0, 0, DateTimeKind.Utc);

    private static VersionIdentity Installed(int major = 16, int minor = 2, string build = "") => new()
    {
        ResolutionStatus = VersionResolutionStatus.Resolved,
        Family = "PostgreSQL",
        Major = major,
        Minor = minor,
        Build = build
    };

    private static ReleaseCatalogEntry Entry(int major, int minor, string build, ReleaseChannel channel = ReleaseChannel.GA) => new()
    {
        Engine = DatabaseEngine.PostgreSql,
        Family = "PostgreSQL",
        Major = major,
        Minor = minor,
        Build = build,
        Channel = channel
    };

    [Fact]
    public void UnresolvedInstalled_NeverComputesBranchOrGaFacts()
    {
        var installed = new VersionIdentity { ResolutionStatus = VersionResolutionStatus.Unresolved, RawVersionText = "garbage" };
        var result = PatchBaselineComparer.Compare(installed, [Entry(16, 2, "1")], null, Now);

        Assert.Null(result.LatestApplicableBranchPatch);
        Assert.Null(result.NewestRelevantGA);
        Assert.Contains("Unresolved", result.InstalledLabel);
    }

    [Fact]
    public void LatestApplicableBranchPatch_PicksHighestBuildWithinSameMajorMinor()
    {
        var candidates = new List<ReleaseCatalogEntry> { Entry(16, 2, "1"), Entry(16, 2, "4"), Entry(16, 3, "0") };
        var result = PatchBaselineComparer.Compare(Installed(), candidates, null, Now);

        Assert.NotNull(result.LatestApplicableBranchPatch);
        Assert.Equal("4", result.LatestApplicableBranchPatch!.Build);
    }

    [Fact]
    public void NewestRelevantGA_PicksHighestMajorMinorAmongGaChannel()
    {
        var candidates = new List<ReleaseCatalogEntry>
        {
            Entry(16, 2, "1"),
            Entry(17, 0, "0"),
            Entry(18, 0, "0", ReleaseChannel.Preview)
        };
        var result = PatchBaselineComparer.Compare(Installed(), candidates, null, Now);

        Assert.NotNull(result.NewestRelevantGA);
        Assert.Equal(17, result.NewestRelevantGA!.Major);
    }

    [Fact]
    public void PreviewExcludedByDefault_EvenIfNewest()
    {
        var candidates = new List<ReleaseCatalogEntry> { Entry(18, 0, "0", ReleaseChannel.Preview) };
        var result = PatchBaselineComparer.Compare(Installed(), candidates, null, Now);

        Assert.Null(result.NewestRelevantGA);
    }

    [Fact]
    public void PreviewIncluded_WhenExplicitlyRequested()
    {
        var candidates = new List<ReleaseCatalogEntry> { Entry(18, 0, "0", ReleaseChannel.Preview) };
        var result = PatchBaselineComparer.Compare(Installed(), candidates, null, Now, includePreview: true);

        Assert.NotNull(result.NewestRelevantGA);
    }

    [Fact]
    public void NoApprovedBaseline_ApprovedTargetIsNull_NeverInventedFromNewestGa()
    {
        var candidates = new List<ReleaseCatalogEntry> { Entry(17, 0, "0") };
        var result = PatchBaselineComparer.Compare(Installed(), candidates, null, Now);

        Assert.Null(result.ApprovedTarget);
    }

    [Fact]
    public void ActiveUnexpiredBaseline_SuppliesApprovedTarget()
    {
        var target = Entry(16, 4, "0");
        target.Id = 42;
        var baseline = new ApprovedPatchBaseline { IsActive = true, TargetReleaseCatalogEntry = target, TargetReleaseCatalogEntryId = 42 };
        var result = PatchBaselineComparer.Compare(Installed(), [target], baseline, Now);

        Assert.Same(target, result.ApprovedTarget);
        Assert.False(result.IsMajorUpgradeFromApproved);
    }

    [Fact]
    public void ExpiredBaseline_DoesNotSupplyApprovedTarget()
    {
        var target = Entry(16, 4, "0");
        var baseline = new ApprovedPatchBaseline { IsActive = true, TargetReleaseCatalogEntry = target, ExpiresAtUtc = Now.AddDays(-1) };
        var result = PatchBaselineComparer.Compare(Installed(), [target], baseline, Now);

        Assert.Null(result.ApprovedTarget);
        Assert.Contains("expired", result.Notes);
    }

    [Fact]
    public void InactiveBaseline_DoesNotSupplyApprovedTarget()
    {
        var target = Entry(16, 4, "0");
        var baseline = new ApprovedPatchBaseline { IsActive = false, TargetReleaseCatalogEntry = target };
        var result = PatchBaselineComparer.Compare(Installed(), [target], baseline, Now);

        Assert.Null(result.ApprovedTarget);
    }

    [Fact]
    public void ApprovedTargetDifferentMajor_FlagsMajorUpgrade()
    {
        var target = Entry(17, 0, "0");
        var baseline = new ApprovedPatchBaseline { IsActive = true, TargetReleaseCatalogEntry = target };
        var result = PatchBaselineComparer.Compare(Installed(major: 16), [target], baseline, Now);

        Assert.True(result.IsMajorUpgradeFromApproved);
    }

    [Fact]
    public void ApprovedTargetSameMajor_IsPatchNotMajorUpgrade()
    {
        var target = Entry(16, 4, "0");
        var baseline = new ApprovedPatchBaseline { IsActive = true, TargetReleaseCatalogEntry = target };
        var result = PatchBaselineComparer.Compare(Installed(major: 16), [target], baseline, Now);

        Assert.False(result.IsMajorUpgradeFromApproved);
    }
}
