using DBTracker.Core.Entities;
using DBTracker.Core.Enums;
using DBTracker.Core.Services;
using Xunit;

namespace DBTracker.UnitTests;

public class MaintenanceEvidenceStalenessClassifierTests
{
    private static readonly DateTime Now = new(2026, 11, 10, 12, 0, 0, DateTimeKind.Utc);

    [Fact]
    public void NeverRun_IsMissing()
    {
        var record = new MaintenanceEvidenceRecord { RanAtUtc = null };
        Assert.Equal(MaintenanceEvidenceStatus.Missing, MaintenanceEvidenceStalenessClassifier.Classify(record, Now));
    }

    [Fact]
    public void FailedOutcome_IsFailed_RegardlessOfRecency()
    {
        var record = new MaintenanceEvidenceRecord { RanAtUtc = Now.AddMinutes(-1), Outcome = MaintenanceOutcome.Failed, ExpectedIntervalHours = 24 };
        Assert.Equal(MaintenanceEvidenceStatus.Failed, MaintenanceEvidenceStalenessClassifier.Classify(record, Now));
    }

    [Fact]
    public void PassedButNoSchedule_IsUnknown_NeverAssumedCurrent()
    {
        var record = new MaintenanceEvidenceRecord { RanAtUtc = Now.AddMinutes(-1), Outcome = MaintenanceOutcome.Passed, ExpectedIntervalHours = null };
        Assert.Equal(MaintenanceEvidenceStatus.Unknown, MaintenanceEvidenceStalenessClassifier.Classify(record, Now));
    }

    [Fact]
    public void PassedWithinSchedule_IsCurrent()
    {
        var record = new MaintenanceEvidenceRecord { RanAtUtc = Now.AddHours(-1), Outcome = MaintenanceOutcome.Passed, ExpectedIntervalHours = 24 };
        Assert.Equal(MaintenanceEvidenceStatus.Current, MaintenanceEvidenceStalenessClassifier.Classify(record, Now));
    }

    [Fact]
    public void PassedButOverdueForNextRun_IsStale_DistinctFromFailed()
    {
        var record = new MaintenanceEvidenceRecord { RanAtUtc = Now.AddHours(-48), Outcome = MaintenanceOutcome.Passed, ExpectedIntervalHours = 24 };
        Assert.Equal(MaintenanceEvidenceStatus.Stale, MaintenanceEvidenceStalenessClassifier.Classify(record, Now));
    }

    [Fact]
    public void UnknownOutcome_StaysUnknown_EvenWithASchedule()
    {
        var record = new MaintenanceEvidenceRecord { RanAtUtc = Now.AddHours(-1), Outcome = MaintenanceOutcome.Unknown, ExpectedIntervalHours = 24 };
        Assert.Equal(MaintenanceEvidenceStatus.Unknown, MaintenanceEvidenceStalenessClassifier.Classify(record, Now));
    }
}
