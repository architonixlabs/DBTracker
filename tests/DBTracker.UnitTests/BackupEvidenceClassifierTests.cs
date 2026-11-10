using DBTracker.Core.Entities;
using DBTracker.Core.Enums;
using DBTracker.Core.Services;
using Xunit;

namespace DBTracker.UnitTests;

public class BackupEvidenceClassifierTests
{
    private static readonly DateTime T = new(2026, 11, 10, 9, 0, 0, DateTimeKind.Utc);

    [Fact]
    public void FailedBackup_HasNoEvidenceLevel()
    {
        var record = new BackupRecord { TimestampUtc = T, Status = BackupStatus.Failed, Source = BackupSource.Manual };
        var level = BackupEvidenceClassifier.Classify(record, Array.Empty<RestoreEvent>());
        Assert.Null(level);
    }

    [Fact]
    public void DiscoveredSuccess_WithNoRestore_IsDiscovered()
    {
        var record = new BackupRecord { TimestampUtc = T, Status = BackupStatus.Success, Source = BackupSource.Discovered };
        var level = BackupEvidenceClassifier.Classify(record, Array.Empty<RestoreEvent>());
        Assert.Equal(EvidenceLevel.Discovered, level);
    }

    [Fact]
    public void ManualSuccess_WithNoRestore_IsReportedSuccess()
    {
        var record = new BackupRecord { TimestampUtc = T, Status = BackupStatus.Success, Source = BackupSource.Manual };
        var level = BackupEvidenceClassifier.Classify(record, Array.Empty<RestoreEvent>());
        Assert.Equal(EvidenceLevel.ReportedSuccess, level);
    }

    [Fact]
    public void SuccessfulRestoreAfterBackup_PromotesToRestoreTested()
    {
        var record = new BackupRecord { TimestampUtc = T, Status = BackupStatus.Success, Source = BackupSource.Discovered };
        var restores = new[] { new RestoreEvent { TimestampUtc = T.AddHours(1), Outcome = RestoreOutcome.Success } };

        var level = BackupEvidenceClassifier.Classify(record, restores);

        Assert.Equal(EvidenceLevel.RestoreTested, level);
    }

    [Fact]
    public void FailedRestore_DoesNotPromote()
    {
        var record = new BackupRecord { TimestampUtc = T, Status = BackupStatus.Success, Source = BackupSource.Manual };
        var restores = new[] { new RestoreEvent { TimestampUtc = T.AddHours(1), Outcome = RestoreOutcome.Failed } };

        var level = BackupEvidenceClassifier.Classify(record, restores);

        Assert.Equal(EvidenceLevel.ReportedSuccess, level);
    }

    [Fact]
    public void RestoreBeforeTheBackupWasTaken_DoesNotPromote()
    {
        var record = new BackupRecord { TimestampUtc = T, Status = BackupStatus.Success, Source = BackupSource.Discovered };
        var restores = new[] { new RestoreEvent { TimestampUtc = T.AddHours(-1), Outcome = RestoreOutcome.Success } };

        var level = BackupEvidenceClassifier.Classify(record, restores);

        Assert.Equal(EvidenceLevel.Discovered, level);
    }
}
