using DBTracker.Core.Enums;
using DBTracker.Core.Services;
using Xunit;

namespace DBTracker.UnitTests;

public class AlertEvaluatorTests
{
    [Fact]
    public void OnTrack_NeverNotifies()
    {
        Assert.False(AlertEvaluator.ShouldNotify(null, BackupHealth.OnTrack));
        Assert.False(AlertEvaluator.ShouldNotify(BackupHealth.Failed, BackupHealth.OnTrack));
    }

    [Theory]
    [InlineData(BackupHealth.Overdue)]
    [InlineData(BackupHealth.Failed)]
    [InlineData(BackupHealth.NeverBackedUp)]
    public void FirstTimeAlertable_Notifies(BackupHealth health)
    {
        Assert.True(AlertEvaluator.ShouldNotify(null, health));
    }

    [Fact]
    public void NeverBackedUp_WithinOnboardingGrace_Suppressed()
    {
        Assert.False(AlertEvaluator.ShouldNotify(null, BackupHealth.NeverBackedUp, suppressNeverBackedUp: true));
    }

    [Fact]
    public void SameAlertableHealthAsLastNotified_DoesNotReNotify()
    {
        Assert.False(AlertEvaluator.ShouldNotify(BackupHealth.Overdue, BackupHealth.Overdue));
        Assert.False(AlertEvaluator.ShouldNotify(BackupHealth.Failed, BackupHealth.Failed));
        Assert.False(AlertEvaluator.ShouldNotify(BackupHealth.NeverBackedUp, BackupHealth.NeverBackedUp));
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

    [Fact]
    public void NextNotifiedState_ClearsForOnTrack()
    {
        Assert.Null(AlertEvaluator.NextNotifiedState(BackupHealth.OnTrack));
    }

    [Theory]
    [InlineData(BackupHealth.Overdue)]
    [InlineData(BackupHealth.Failed)]
    [InlineData(BackupHealth.NeverBackedUp)]
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

    [Fact]
    public void IsWithinOnboardingGrace_RecentlyCreated_IsTrue()
    {
        var created = new DateTime(2026, 11, 10, 12, 0, 0, DateTimeKind.Utc);
        Assert.True(AlertEvaluator.IsWithinOnboardingGrace(created, created.AddHours(1), graceHours: 24));
    }

    [Fact]
    public void IsWithinOnboardingGrace_PastGraceWindow_IsFalse()
    {
        var created = new DateTime(2026, 11, 10, 12, 0, 0, DateTimeKind.Utc);
        Assert.False(AlertEvaluator.IsWithinOnboardingGrace(created, created.AddHours(25), graceHours: 24));
    }

    [Fact]
    public void IsWithinOnboardingGrace_ZeroOrNegativeGrace_DisablesGraceEntirely()
    {
        var created = DateTime.UtcNow;
        Assert.False(AlertEvaluator.IsWithinOnboardingGrace(created, created, graceHours: 0));
        Assert.False(AlertEvaluator.IsWithinOnboardingGrace(created, created, graceHours: -5));
    }
}
