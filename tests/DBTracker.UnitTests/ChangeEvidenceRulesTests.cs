using DBTracker.Core.Entities;
using DBTracker.Core.Enums;
using DBTracker.Core.Services;
using Xunit;

namespace DBTracker.UnitTests;

public class ChangeEvidenceRulesTests
{
    private static readonly DateTime Now = new(2026, 11, 11, 12, 0, 0, DateTimeKind.Utc);
    private static readonly DateTime Started = Now.AddHours(-6);

    [Fact]
    public void JournalEntry_EarlierThanNow_IsAllowed()
    {
        Assert.Null(ChangeEvidenceRules.ValidateJournalEntry("Stopped writes", Now.AddHours(-3), Now));
    }

    [Fact]
    public void JournalEntry_InTheFuture_IsRefused()
    {
        Assert.NotNull(ChangeEvidenceRules.ValidateJournalEntry("Stopped writes", Now.AddMinutes(30), Now));
    }

    [Fact]
    public void JournalEntry_WithinClockSkew_IsAllowed()
    {
        Assert.Null(ChangeEvidenceRules.ValidateJournalEntry("Stopped writes", Now.AddMinutes(2), Now));
    }

    [Fact]
    public void JournalEntry_WithoutDescription_IsRefused()
    {
        Assert.NotNull(ChangeEvidenceRules.ValidateJournalEntry("  ", Now, Now));
    }

    [Fact]
    public void PassedOutcome_WithNoLinkedRecord_IsRefused()
    {
        var errors = ChangeEvidenceRules.ValidateLinkedEvidence(EvidenceCheckOutcome.Passed, 1, null, null, Started);

        Assert.Single(errors);
    }

    [Fact]
    public void NotPerformedOutcome_WithNoLink_IsFine()
    {
        Assert.Empty(ChangeEvidenceRules.ValidateLinkedEvidence(EvidenceCheckOutcome.NotPerformed, 1, null, null, Started));
    }

    [Fact]
    public void SuccessfulBackupAfterTheChange_Passes()
    {
        var backup = new BackupRecord { Id = 5, DatabaseInstanceId = 1, Status = BackupStatus.Success, TimestampUtc = Started.AddHours(1) };

        Assert.Empty(ChangeEvidenceRules.ValidateLinkedEvidence(EvidenceCheckOutcome.Passed, 1, backup, null, Started));
    }

    [Fact]
    public void BackupTakenBeforeTheChange_IsRefused()
    {
        var backup = new BackupRecord { Id = 5, DatabaseInstanceId = 1, Status = BackupStatus.Success, TimestampUtc = Started.AddHours(-1) };

        var errors = ChangeEvidenceRules.ValidateLinkedEvidence(EvidenceCheckOutcome.Passed, 1, backup, null, Started);

        Assert.Contains(errors, e => e.Contains("before the change started"));
    }

    [Fact]
    public void FailedBackup_OrAnotherInstancesBackup_IsRefused()
    {
        var failed = new BackupRecord { DatabaseInstanceId = 1, Status = BackupStatus.Failed, TimestampUtc = Started.AddHours(1) };
        var other = new BackupRecord { DatabaseInstanceId = 2, Status = BackupStatus.Success, TimestampUtc = Started.AddHours(1) };

        Assert.NotEmpty(ChangeEvidenceRules.ValidateLinkedEvidence(EvidenceCheckOutcome.Passed, 1, failed, null, Started));
        Assert.NotEmpty(ChangeEvidenceRules.ValidateLinkedEvidence(EvidenceCheckOutcome.Passed, 1, other, null, Started));
    }

    [Fact]
    public void FailedRestore_IsRefused_SuccessfulRestore_Passes()
    {
        var failed = new RestoreEvent { DatabaseInstanceId = 1, Outcome = RestoreOutcome.Failed, TimestampUtc = Started.AddHours(1) };
        var ok = new RestoreEvent { DatabaseInstanceId = 1, Outcome = RestoreOutcome.Success, TimestampUtc = Started.AddHours(1) };

        Assert.NotEmpty(ChangeEvidenceRules.ValidateLinkedEvidence(EvidenceCheckOutcome.Passed, 1, null, failed, Started));
        Assert.Empty(ChangeEvidenceRules.ValidateLinkedEvidence(EvidenceCheckOutcome.Passed, 1, null, ok, Started));
    }

    [Fact]
    public void UnknownChangeStart_DoesNotRejectOnTime()
    {
        var backup = new BackupRecord { DatabaseInstanceId = 1, Status = BackupStatus.Success, TimestampUtc = Now.AddYears(-1) };

        Assert.Empty(ChangeEvidenceRules.ValidateLinkedEvidence(EvidenceCheckOutcome.Passed, 1, backup, null, null));
    }

    [Fact]
    public void Snapshots_RecordDeclaredVersionAndObservedBuild()
    {
        var instance = new DatabaseInstance { Version = "2019" };
        var identity = new VersionIdentity { Family = "SQL Server", Major = 15, Build = "15.0.4355", ResolutionStatus = VersionResolutionStatus.Resolved };
        var validation = new ChangeValidation { ObservedTargetBuildText = "16.0.1000", ApplicationTestsOutcome = EvidenceCheckOutcome.Passed };
        var backup = new BackupRecord { Id = 9, Status = BackupStatus.Success, TimestampUtc = Now };

        var before = ChangeEvidenceRules.BuildBeforeSnapshot(instance, identity, Now);
        var after = ChangeEvidenceRules.BuildAfterSnapshot(validation, backup, null, Now);

        Assert.Contains("\"declaredVersion\":\"2019\"", before);
        Assert.Contains("15.0.4355", before);
        Assert.Contains("\"observedBuild\":\"16.0.1000\"", after);
        Assert.Contains("\"Id\":9", after);
    }
}
