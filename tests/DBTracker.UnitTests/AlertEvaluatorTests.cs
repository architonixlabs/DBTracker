using DBTracker.Core.Enums;
using DBTracker.Core.Services;
using Xunit;

namespace DBTracker.UnitTests;

public class AlertEvaluatorTests
{
    [Theory]
    [InlineData(BackupHealth.OnTrack)]
    [InlineData(BackupHealth.NeverBackedUp)]
    public void HealthyOrUnmonitorable_NeverNotifies(BackupHealth health)
    {
        Assert.False(AlertEvaluator.ShouldNotify(null, health));
        Assert.False(AlertEvaluator.ShouldNotify(BackupHealth.Failed, health));
    }

    [Theory]
    [InlineData(BackupHealth.Overdue)]
    [InlineData(BackupHealth.Failed)]
    public void FirstTimeOverdueOrFailed_Notifies(BackupHealth health)
    {
        Assert.True(AlertEvaluator.ShouldNotify(null, health));
    }

    [Fact]
    public void SameAlertableHealthAsLastNotified_DoesNotReNotify()
    {
        Assert.False(AlertEvaluator.ShouldNotify(BackupHealth.Overdue, BackupHealth.Overdue));
        Assert.False(AlertEvaluator.ShouldNotify(BackupHealth.Failed, BackupHealth.Failed));
    }

    [Fact]
    public void WorseningFromOverdueToFailed_NotifiesAgain()
    {
        Assert.True(AlertEvaluator.ShouldNotify(BackupHealth.Overdue, BackupHealth.Failed));
    }

    [Fact]
    public void RecoveringThenRelapsing_NotifiesAgain()
    {
        // Simulates the full sweep lifecycle: Failed -> recovers (state cleared) -> Failed again.
        var afterFirstFailure = AlertEvaluator.NextNotifiedState(BackupHealth.Failed);
        Assert.Equal(BackupHealth.Failed, afterFirstFailure);

        var afterRecovery = AlertEvaluator.NextNotifiedState(BackupHealth.OnTrack);
        Assert.Null(afterRecovery);

        Assert.True(AlertEvaluator.ShouldNotify(afterRecovery, BackupHealth.Failed));
    }

    [Theory]
    [InlineData(BackupHealth.OnTrack)]
    [InlineData(BackupHealth.NeverBackedUp)]
    public void NextNotifiedState_ClearsForHealthyStates(BackupHealth health)
    {
        Assert.Null(AlertEvaluator.NextNotifiedState(health));
    }

    [Theory]
    [InlineData(BackupHealth.Overdue)]
    [InlineData(BackupHealth.Failed)]
    public void NextNotifiedState_RemembersAlertableStates(BackupHealth health)
    {
        Assert.Equal(health, AlertEvaluator.NextNotifiedState(health));
    }

    [Fact]
    public void IsSuppressedByMaintenance_NullWindow_NeverSuppresses()
    {
        Assert.False(AlertEvaluator.IsSuppressedByMaintenance(null, DateTime.UtcNow));
    }

    [Fact]
    public void IsSuppressedByMaintenance_FutureWindow_Suppresses()
    {
        var now = new DateTime(2026, 11, 10, 12, 0, 0, DateTimeKind.Utc);
        Assert.True(AlertEvaluator.IsSuppressedByMaintenance(now.AddHours(1), now));
    }

    [Fact]
    public void IsSuppressedByMaintenance_ExpiredWindow_DoesNotSuppress()
    {
        var now = new DateTime(2026, 11, 10, 12, 0, 0, DateTimeKind.Utc);
        Assert.False(AlertEvaluator.IsSuppressedByMaintenance(now.AddHours(-1), now));
    }
}
