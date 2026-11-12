using DBTracker.Core.Entities;
using DBTracker.Core.Services;
using Xunit;

namespace DBTracker.UnitTests;

public class ReadinessExceptionRenewalTests
{
    private static readonly DateTime Now = new(2026, 11, 11, 0, 0, 0, DateTimeKind.Utc);

    [Fact]
    public void Expiry_InThePast_IsRefused()
    {
        Assert.NotNull(ReadinessExceptionEvaluator.ValidateExpiry(Now.AddDays(-1), Now));
    }

    [Fact]
    public void Expiry_BeyondTheMaximum_IsRefused()
    {
        Assert.NotNull(ReadinessExceptionEvaluator.ValidateExpiry(Now.AddDays(ReadinessExceptionEvaluator.MaxDurationDays + 1), Now));
    }

    [Fact]
    public void Expiry_ChosenByTheUser_WithinRange_IsAccepted()
    {
        Assert.Null(ReadinessExceptionEvaluator.ValidateExpiry(Now.AddDays(90), Now));
    }

    [Fact]
    public void IsLatestInChain_FalseOnceRenewed()
    {
        var original = new ReadinessException { Id = 1 };
        var renewal = new ReadinessException { Id = 2, RenewalOfExceptionId = 1 };

        Assert.False(ReadinessExceptionEvaluator.IsLatestInChain(original, [original, renewal]));
        Assert.True(ReadinessExceptionEvaluator.IsLatestInChain(renewal, [original, renewal]));
    }

    [Fact]
    public void Targets_MatchTheNamesTheGatesLookUp()
    {
        Assert.Contains("MigrationCampaign", ReadinessExceptionTargets.All);
        Assert.Contains("RetirementChecklist", ReadinessExceptionTargets.All);
        Assert.Contains("DatabaseInstance", ReadinessExceptionTargets.All);
        Assert.Equal("Change window", ReadinessExceptionTargets.Describe(ReadinessExceptionTargets.ChangeWindow));
    }
}
