using DBTracker.Core.Entities;
using DBTracker.Core.Enums;
using DBTracker.Core.Services;
using Xunit;

namespace DBTracker.UnitTests;

public class EntitlementStatusClassifierTests
{
    private static readonly DateTime Now = new(2026, 11, 10, 0, 0, 0, DateTimeKind.Utc);

    private static SupportContract Contract(DateTime? start, DateTime? end, SupportCoverageType type = SupportCoverageType.Standard) => new()
    {
        Vendor = "Acme Vendor",
        CoverageType = type,
        CoverageStartUtc = start,
        CoverageEndUtc = end
    };

    [Fact]
    public void NoContracts_IsNoContractOnFile_NeverExpired()
    {
        var result = EntitlementStatusClassifier.Classify([], Now);

        Assert.Equal(EntitlementStatus.NoContractOnFile, result.Status);
        Assert.Null(result.MostFavorableContract);
    }

    [Fact]
    public void ActiveContract_FarFromExpiry_IsActive()
    {
        var contract = Contract(Now.AddDays(-30), Now.AddDays(365));
        var result = EntitlementStatusClassifier.Classify([contract], Now);

        Assert.Equal(EntitlementStatus.Active, result.Status);
        Assert.Same(contract, result.MostFavorableContract);
    }

    [Fact]
    public void ActiveContract_WithinThreshold_IsExpiringSoon()
    {
        var contract = Contract(Now.AddDays(-300), Now.AddDays(30));
        var result = EntitlementStatusClassifier.Classify([contract], Now, expiringSoonThresholdDays: 60);

        Assert.Equal(EntitlementStatus.ExpiringSoon, result.Status);
        Assert.NotNull(result.DaysRemaining);
        Assert.True(result.DaysRemaining > 0);
    }

    [Fact]
    public void LapsedContract_IsExpired_NotNoContractOnFile()
    {
        var contract = Contract(Now.AddDays(-400), Now.AddDays(-10));
        var result = EntitlementStatusClassifier.Classify([contract], Now);

        Assert.Equal(EntitlementStatus.Expired, result.Status);
        Assert.NotEqual(EntitlementStatus.NoContractOnFile, result.Status);
    }

    [Fact]
    public void OpenEndedContract_CurrentlyCovering_IsActiveWithNoDaysRemaining()
    {
        var contract = Contract(Now.AddDays(-10), null);
        var result = EntitlementStatusClassifier.Classify([contract], Now);

        Assert.Equal(EntitlementStatus.Active, result.Status);
        Assert.Null(result.DaysRemaining);
    }

    [Fact]
    public void MultipleContracts_PrefersTheOneCurrentlyCoveringWithLatestEnd()
    {
        var expired = Contract(Now.AddDays(-400), Now.AddDays(-30));
        var active = Contract(Now.AddDays(-30), Now.AddDays(400));
        var result = EntitlementStatusClassifier.Classify([expired, active], Now);

        Assert.Equal(EntitlementStatus.Active, result.Status);
        Assert.Same(active, result.MostFavorableContract);
    }

    [Fact]
    public void EntitlementStatus_IsIndependentOfAnyLifecycleConcept()
    {
        // This classifier takes only contracts + now — there is no vendor-lifecycle/support-phase
        // input available to pass it at all, which is the point: D21 entitlement and D02/D03
        // lifecycle status can never be computed from each other by construction.
        var contract = Contract(Now.AddDays(-1), Now.AddDays(1));
        var result = EntitlementStatusClassifier.Classify([contract], Now);

        Assert.Equal(EntitlementStatus.ExpiringSoon, result.Status);
    }
}
