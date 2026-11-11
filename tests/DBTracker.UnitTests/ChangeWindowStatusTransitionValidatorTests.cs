using DBTracker.Core.Enums;
using DBTracker.Core.Services;
using Xunit;

namespace DBTracker.UnitTests;

public class ChangeWindowStatusTransitionValidatorTests
{
    [Theory]
    [InlineData(ChangeWindowStatus.Planned, ChangeWindowStatus.Completed)]
    [InlineData(ChangeWindowStatus.Planned, ChangeWindowStatus.RolledBack)]
    [InlineData(ChangeWindowStatus.Approved, ChangeWindowStatus.Completed)]
    [InlineData(ChangeWindowStatus.Approved, ChangeWindowStatus.RolledBack)]
    public void FinishingWithoutStarting_IsRefused(ChangeWindowStatus from, ChangeWindowStatus to)
    {
        Assert.False(ChangeWindowStatusTransitionValidator.IsValidTransition(from, to));
        Assert.Contains("can only move to", ChangeWindowStatusTransitionValidator.DescribeInvalidReason(from, to));
    }

    [Theory]
    [InlineData(ChangeWindowStatus.Planned, ChangeWindowStatus.Approved)]
    [InlineData(ChangeWindowStatus.Planned, ChangeWindowStatus.InProgress)]
    [InlineData(ChangeWindowStatus.Planned, ChangeWindowStatus.Cancelled)]
    [InlineData(ChangeWindowStatus.Approved, ChangeWindowStatus.InProgress)]
    [InlineData(ChangeWindowStatus.Approved, ChangeWindowStatus.Planned)]
    [InlineData(ChangeWindowStatus.Approved, ChangeWindowStatus.Cancelled)]
    [InlineData(ChangeWindowStatus.InProgress, ChangeWindowStatus.Completed)]
    [InlineData(ChangeWindowStatus.InProgress, ChangeWindowStatus.RolledBack)]
    public void TheNormalFlow_IsAllowed(ChangeWindowStatus from, ChangeWindowStatus to) =>
        Assert.True(ChangeWindowStatusTransitionValidator.IsValidTransition(from, to));

    [Theory]
    [InlineData(ChangeWindowStatus.Completed)]
    [InlineData(ChangeWindowStatus.RolledBack)]
    [InlineData(ChangeWindowStatus.Cancelled)]
    public void FinalStatuses_HaveNoNextStatus(ChangeWindowStatus from)
    {
        Assert.Empty(ChangeWindowStatusTransitionValidator.AllowedNextStatuses(from));
        Assert.Contains("is final", ChangeWindowStatusTransitionValidator.DescribeInvalidReason(from, ChangeWindowStatus.Planned));
    }

    [Fact]
    public void InProgress_CannotBeCancelled_OnlyRolledBack() =>
        Assert.False(ChangeWindowStatusTransitionValidator.IsValidTransition(ChangeWindowStatus.InProgress, ChangeWindowStatus.Cancelled));

    [Fact]
    public void SameStatus_IsNotATransition() =>
        Assert.False(ChangeWindowStatusTransitionValidator.IsValidTransition(ChangeWindowStatus.Planned, ChangeWindowStatus.Planned));
}
