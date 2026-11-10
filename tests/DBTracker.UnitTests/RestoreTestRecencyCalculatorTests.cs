using DBTracker.Core.Enums;
using DBTracker.Core.Services;
using Xunit;

namespace DBTracker.UnitTests;

public class RestoreTestRecencyCalculatorTests
{
    private static readonly DateTime Now = new(2026, 11, 10, 9, 0, 0, DateTimeKind.Utc);

    [Fact]
    public void NullLastRestore_IsNeverTested_WithNoNextDue()
    {
        var result = RestoreTestRecencyCalculator.Compute(null, 90, Now);

        Assert.Equal(RestoreTestRecency.NeverTested, result.Status);
        Assert.Null(result.NextDueUtc);
    }

    [Fact]
    public void LastRestoreWithinInterval_IsCurrent()
    {
        var result = RestoreTestRecencyCalculator.Compute(Now.AddDays(-10), 90, Now);

        Assert.Equal(RestoreTestRecency.Current, result.Status);
        Assert.Equal(Now.AddDays(-10).AddDays(90), result.NextDueUtc);
    }

    [Fact]
    public void LastRestoreExactlyAtDeadline_IsOverdue()
    {
        var lastRestore = Now.AddDays(-90);
        var result = RestoreTestRecencyCalculator.Compute(lastRestore, 90, Now);

        Assert.Equal(RestoreTestRecency.Overdue, result.Status);
    }

    [Fact]
    public void LastRestoreBeyondInterval_IsOverdue()
    {
        var result = RestoreTestRecencyCalculator.Compute(Now.AddDays(-200), 90, Now);

        Assert.Equal(RestoreTestRecency.Overdue, result.Status);
    }
}
