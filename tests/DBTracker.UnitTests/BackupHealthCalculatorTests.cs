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
}
