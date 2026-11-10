using DBTracker.Core.Entities;
using DBTracker.Core.Enums;
using DBTracker.Core.Services;
using Xunit;

namespace DBTracker.UnitTests;

public class BackupHealthCalculatorTests
{
    private static readonly DateTime Now = new(2026, 11, 9, 12, 0, 0, DateTimeKind.Utc);

    private static DatabaseInstance Instance(int expectedIntervalHours = 24) => new()
    {
        Id = 1,
        Name = "TEST-DB",
        ExpectedBackupIntervalHours = expectedIntervalHours
    };

    [Fact]
    public void NoRecords_IsNeverBackedUp()
    {
        var health = BackupHealthCalculator.Compute(Instance(), Array.Empty<BackupRecord>(), Now);
        Assert.Equal(BackupHealth.NeverBackedUp, health);
    }

    [Fact]
    public void RecentSuccessWithinWindow_IsOnTrack()
    {
        var records = new[]
        {
            new BackupRecord { TimestampUtc = Now.AddHours(-4), Status = BackupStatus.Success }
        };

        var health = BackupHealthCalculator.Compute(Instance(expectedIntervalHours: 24), records, Now);

        Assert.Equal(BackupHealth.OnTrack, health);
    }

    [Fact]
    public void SuccessOutsideWindow_IsOverdue()
    {
        var records = new[]
        {
            new BackupRecord { TimestampUtc = Now.AddHours(-30), Status = BackupStatus.Success }
        };

        var health = BackupHealthCalculator.Compute(Instance(expectedIntervalHours: 24), records, Now);

        Assert.Equal(BackupHealth.Overdue, health);
    }

    [Fact]
    public void ExactlyAtDeadline_IsOnTrack()
    {
        var records = new[]
        {
            new BackupRecord { TimestampUtc = Now.AddHours(-24), Status = BackupStatus.Success }
        };

        var health = BackupHealthCalculator.Compute(Instance(expectedIntervalHours: 24), records, Now);

        Assert.Equal(BackupHealth.OnTrack, health);
    }

    [Fact]
    public void MostRecentFailed_IsFailed_EvenWithOlderSuccess()
    {
        var records = new[]
        {
            new BackupRecord { TimestampUtc = Now.AddHours(-1), Status = BackupStatus.Failed },
            new BackupRecord { TimestampUtc = Now.AddHours(-25), Status = BackupStatus.Success }
        };

        var health = BackupHealthCalculator.Compute(Instance(expectedIntervalHours: 24), records, Now);

        Assert.Equal(BackupHealth.Failed, health);
    }

    [Fact]
    public void MostRecentMissed_IsFailed()
    {
        var records = new[]
        {
            new BackupRecord { TimestampUtc = Now.AddHours(-1), Status = BackupStatus.Missed }
        };

        var health = BackupHealthCalculator.Compute(Instance(), records, Now);

        Assert.Equal(BackupHealth.Failed, health);
    }

    [Fact]
    public void NewerSuccessAfterOlderFailure_IsOnTrack()
    {
        var records = new[]
        {
            new BackupRecord { TimestampUtc = Now.AddHours(-2), Status = BackupStatus.Success },
            new BackupRecord { TimestampUtc = Now.AddHours(-26), Status = BackupStatus.Failed }
        };

        var health = BackupHealthCalculator.Compute(Instance(expectedIntervalHours: 24), records, Now);

        Assert.Equal(BackupHealth.OnTrack, health);
    }

    [Fact]
    public void LastSuccessfulBackupUtc_IgnoresFailedAndMissed_ReturnsMostRecentSuccess()
    {
        var records = new[]
        {
            new BackupRecord { TimestampUtc = Now.AddHours(-1), Status = BackupStatus.Failed },
            new BackupRecord { TimestampUtc = Now.AddHours(-10), Status = BackupStatus.Success },
            new BackupRecord { TimestampUtc = Now.AddHours(-34), Status = BackupStatus.Success }
        };

        var last = BackupHealthCalculator.LastSuccessfulBackupUtc(records);

        Assert.Equal(Now.AddHours(-10), last);
    }

    [Fact]
    public void LastSuccessfulBackupUtc_NoSuccess_ReturnsNull()
    {
        var records = new[]
        {
            new BackupRecord { TimestampUtc = Now.AddHours(-1), Status = BackupStatus.Failed }
        };

        var last = BackupHealthCalculator.LastSuccessfulBackupUtc(records);

        Assert.Null(last);
    }

    [Theory]
    [InlineData(12)]
    [InlineData(48)]
    [InlineData(72)]
    public void RespectsPerInstanceExpectedInterval(int intervalHours)
    {
        var records = new[]
        {
            new BackupRecord { TimestampUtc = Now.AddHours(-intervalHours + 1), Status = BackupStatus.Success }
        };

        var health = BackupHealthCalculator.Compute(Instance(intervalHours), records, Now);

        Assert.Equal(BackupHealth.OnTrack, health);
    }

    [Fact]
    public void FutureDatedRecord_IsIgnoredWhenEvaluatingAnEarlierInstant()
    {
        // A record timestamped after asOfUtc can't yet be used as evidence for that earlier instant
        // (U04) — otherwise a backdated snapshot could be fooled by data that didn't exist yet.
        var records = new[]
        {
            new BackupRecord { Id = 1, TimestampUtc = Now.AddHours(-30), Status = BackupStatus.Success },
            new BackupRecord { Id = 2, TimestampUtc = Now.AddHours(5), Status = BackupStatus.Success } // future relative to Now
        };

        var health = BackupHealthCalculator.Compute(Instance(expectedIntervalHours: 24), records, Now);

        Assert.Equal(BackupHealth.Overdue, health);
    }

    [Fact]
    public void FutureDatedFailure_DoesNotMakeAPastInstantLookFailed()
    {
        var records = new[]
        {
            new BackupRecord { Id = 1, TimestampUtc = Now.AddHours(-2), Status = BackupStatus.Success },
            new BackupRecord { Id = 2, TimestampUtc = Now.AddHours(5), Status = BackupStatus.Failed } // future
        };

        var health = BackupHealthCalculator.Compute(Instance(expectedIntervalHours: 24), records, Now);

        Assert.Equal(BackupHealth.OnTrack, health);
    }

    [Fact]
    public void OnlyFutureDatedRecordsExist_IsNeverBackedUpAsOfNow()
    {
        var records = new[]
        {
            new BackupRecord { TimestampUtc = Now.AddDays(1), Status = BackupStatus.Success }
        };

        var health = BackupHealthCalculator.Compute(Instance(), records, Now);

        Assert.Equal(BackupHealth.NeverBackedUp, health);
    }

    [Fact]
    public void TiedTimestamps_BreakByIdDescending_ForDeterministicOrdering()
    {
        var records = new[]
        {
            new BackupRecord { Id = 1, TimestampUtc = Now.AddHours(-1), Status = BackupStatus.Success },
            new BackupRecord { Id = 2, TimestampUtc = Now.AddHours(-1), Status = BackupStatus.Failed }
        };

        var health = BackupHealthCalculator.Compute(Instance(), records, Now);

        // Id 2 (inserted later) wins the tie, so a failure logged at the same instant as an earlier
        // success is treated as the authoritative latest attempt.
        Assert.Equal(BackupHealth.Failed, health);
    }

    [Fact]
    public void Explain_ReportsLastSuccessNextDueAndOverdueBy()
    {
        var records = new[]
        {
            new BackupRecord { Id = 1, TimestampUtc = Now.AddHours(-30), Status = BackupStatus.Success }
        };

        var explanation = BackupHealthCalculator.Explain(Instance(expectedIntervalHours: 24), records, Now);

        Assert.Equal(BackupHealth.Overdue, explanation.Health);
        Assert.Equal(Now.AddHours(-30), explanation.LastSuccessUtc);
        Assert.Equal(Now.AddHours(-30).AddHours(24), explanation.NextDueUtc);
        Assert.NotNull(explanation.OverdueBy);
        Assert.Equal(TimeSpan.FromHours(6), explanation.OverdueBy);
        Assert.False(explanation.IsSuppressedByMaintenance);
    }

    [Fact]
    public void Explain_OnTrack_HasNoOverdueDuration()
    {
        var records = new[]
        {
            new BackupRecord { Id = 1, TimestampUtc = Now.AddHours(-4), Status = BackupStatus.Success }
        };

        var explanation = BackupHealthCalculator.Explain(Instance(expectedIntervalHours: 24), records, Now);

        Assert.Null(explanation.OverdueBy);
    }

    [Fact]
    public void Explain_SeparatesMaintenanceSuppressionFromHealthItself()
    {
        var instance = Instance(expectedIntervalHours: 24);
        instance.MaintenanceUntilUtc = Now.AddHours(1);
        var records = new[]
        {
            new BackupRecord { Id = 1, TimestampUtc = Now.AddHours(-30), Status = BackupStatus.Success }
        };

        var explanation = BackupHealthCalculator.Explain(instance, records, Now);

        // Health stays truthfully Overdue even while maintenance suppresses alerting — the two are
        // reported as separate facts, not folded together.
        Assert.Equal(BackupHealth.Overdue, explanation.Health);
        Assert.True(explanation.IsSuppressedByMaintenance);
    }

    [Fact]
    public void Explain_NeverBackedUp_HasNullSuccessAndNextDue()
    {
        var explanation = BackupHealthCalculator.Explain(Instance(), Array.Empty<BackupRecord>(), Now);

        Assert.Equal(BackupHealth.NeverBackedUp, explanation.Health);
        Assert.Null(explanation.LastSuccessUtc);
        Assert.Null(explanation.NextDueUtc);
        Assert.Null(explanation.OverdueBy);
    }
}
