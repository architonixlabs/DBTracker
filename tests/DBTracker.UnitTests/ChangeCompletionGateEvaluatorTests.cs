using DBTracker.Core.Entities;
using DBTracker.Core.Enums;
using DBTracker.Core.Services;
using Xunit;

namespace DBTracker.UnitTests;

public class ChangeCompletionGateEvaluatorTests
{
    private static ChangeValidation PassingValidation() => new()
    {
        ApplicationTestsOutcome = EvidenceCheckOutcome.Passed,
        IntegrityCheckOutcome = EvidenceCheckOutcome.Passed,
        JobsPermissionsOutcome = EvidenceCheckOutcome.Passed,
        HaReplicationStateOutcome = EvidenceCheckOutcome.Passed,
        NewBackupRestoreEvidenceOutcome = EvidenceCheckOutcome.Passed,
        LinkedBackupRecordId = 42
    };

    private static List<ChangeValidationSignoff> PassingSignoffs() =>
    [
        new() { ReviewerScope = "DBA", Outcome = SignoffOutcome.Approved },
        new() { ReviewerScope = "Application owner", Outcome = SignoffOutcome.Approved }
    ];

    private static readonly RehearsalReadinessResult PassingRehearsal = new(RehearsalReadinessStatus.Passed, "ok");

    [Fact]
    public void MissingValidation_Blocks()
    {
        var result = ChangeCompletionGateEvaluator.Evaluate(null, [], PassingRehearsal, hasActiveException: false);

        Assert.False(result.CanComplete);
        Assert.Contains(result.BlockingReasons, r => r.Contains("No post-change validation"));
    }

    [Fact]
    public void EverythingPassing_WithAllSignoffsAndCurrentRehearsal_CanComplete()
    {
        var result = ChangeCompletionGateEvaluator.Evaluate(PassingValidation(), PassingSignoffs(), PassingRehearsal, hasActiveException: false);

        Assert.True(result.CanComplete);
        Assert.Empty(result.BlockingReasons);
        Assert.False(result.UsedException);
    }

    [Fact]
    public void VersionTextEditAlone_NeverCompletesAChange_EvidenceFieldsAreWhatsChecked()
    {
        // A validation record with every evidence field left at its default (NotPerformed) models
        // "someone only edited the version text" — the gate must still block.
        var bareValidation = new ChangeValidation { ObservedTargetBuildText = "16.0.1000" };

        var result = ChangeCompletionGateEvaluator.Evaluate(bareValidation, [], PassingRehearsal, hasActiveException: false);

        Assert.False(result.CanComplete);
        Assert.True(result.BlockingReasons.Count >= 5); // every evidence check plus missing sign-offs
    }

    [Theory]
    [InlineData(nameof(ChangeValidation.ApplicationTestsOutcome))]
    [InlineData(nameof(ChangeValidation.IntegrityCheckOutcome))]
    [InlineData(nameof(ChangeValidation.JobsPermissionsOutcome))]
    [InlineData(nameof(ChangeValidation.HaReplicationStateOutcome))]
    [InlineData(nameof(ChangeValidation.NewBackupRestoreEvidenceOutcome))]
    public void AnySingleFailedCheck_Blocks(string property)
    {
        var validation = PassingValidation();
        typeof(ChangeValidation).GetProperty(property)!.SetValue(validation, EvidenceCheckOutcome.Failed);

        var result = ChangeCompletionGateEvaluator.Evaluate(validation, PassingSignoffs(), PassingRehearsal, hasActiveException: false);

        Assert.False(result.CanComplete);
    }

    [Fact]
    public void MissingRequiredSignoffScope_Blocks()
    {
        var signoffs = new List<ChangeValidationSignoff> { new() { ReviewerScope = "DBA", Outcome = SignoffOutcome.Approved } }; // Application owner missing

        var result = ChangeCompletionGateEvaluator.Evaluate(PassingValidation(), signoffs, PassingRehearsal, hasActiveException: false);

        Assert.False(result.CanComplete);
        Assert.Contains(result.BlockingReasons, r => r.Contains("Application owner"));
    }

    [Fact]
    public void RejectedSignoff_DoesNotCountAsApproval()
    {
        var signoffs = new List<ChangeValidationSignoff>
        {
            new() { ReviewerScope = "DBA", Outcome = SignoffOutcome.Rejected },
            new() { ReviewerScope = "Application owner", Outcome = SignoffOutcome.Approved }
        };

        var result = ChangeCompletionGateEvaluator.Evaluate(PassingValidation(), signoffs, PassingRehearsal, hasActiveException: false);

        Assert.False(result.CanComplete);
        Assert.Contains(result.BlockingReasons, r => r.Contains("DBA"));
    }

    [Fact]
    public void StaleOrFailedRehearsal_Blocks_EvenWhenValidationAndSignoffsPass()
    {
        var staleRehearsal = new RehearsalReadinessResult(RehearsalReadinessStatus.Stale, "old rehearsal");

        var result = ChangeCompletionGateEvaluator.Evaluate(PassingValidation(), PassingSignoffs(), staleRehearsal, hasActiveException: false);

        Assert.False(result.CanComplete);
        Assert.Contains(result.BlockingReasons, r => r.Contains("Rehearsal evidence"));
    }

    [Fact]
    public void NullRehearsalResult_DoesNotBlock_NotEveryChangeNeedsARehearsalCheck()
    {
        var result = ChangeCompletionGateEvaluator.Evaluate(PassingValidation(), PassingSignoffs(), null, hasActiveException: false);

        Assert.True(result.CanComplete);
    }

    [Fact]
    public void AllBlockingReasons_WithActiveException_CanCompleteAnyway_ButReasonsStillReported()
    {
        var result = ChangeCompletionGateEvaluator.Evaluate(null, [], PassingRehearsal, hasActiveException: true);

        Assert.True(result.CanComplete);
        Assert.True(result.UsedException);
        Assert.NotEmpty(result.BlockingReasons); // never silently erased
    }

    [Fact]
    public void PassedBackupEvidence_WithoutALinkedRecord_Blocks()
    {
        var validation = PassingValidation();
        validation.LinkedBackupRecordId = null;

        var result = ChangeCompletionGateEvaluator.Evaluate(validation, PassingSignoffs(), PassingRehearsal, hasActiveException: false);

        Assert.False(result.CanComplete);
        Assert.Contains(result.BlockingReasons, r => r.Contains("no backup or restore record is linked"));
    }

    [Fact]
    public void LinkedRestore_SatisfiesTheEvidenceLink()
    {
        var validation = PassingValidation();
        validation.LinkedBackupRecordId = null;
        validation.LinkedRestoreEventId = 3;

        Assert.True(ChangeCompletionGateEvaluator.Evaluate(validation, PassingSignoffs(), PassingRehearsal, false).CanComplete);
    }

    [Fact]
    public void SamePersonSigningBothRequiredScopes_Blocks()
    {
        List<ChangeValidationSignoff> signoffs =
        [
            new() { ReviewerScope = "DBA", Outcome = SignoffOutcome.Approved, ReviewerUserId = "u1" },
            new() { ReviewerScope = "Application owner", Outcome = SignoffOutcome.Approved, ReviewerUserId = "u1" }
        ];

        var result = ChangeCompletionGateEvaluator.Evaluate(PassingValidation(), signoffs, PassingRehearsal, false);

        Assert.False(result.CanComplete);
        Assert.Contains(result.BlockingReasons, r => r.Contains("different people"));
    }

    [Fact]
    public void DifferentPeopleSigningTheRequiredScopes_CanComplete()
    {
        List<ChangeValidationSignoff> signoffs =
        [
            new() { ReviewerScope = "DBA", Outcome = SignoffOutcome.Approved, ReviewerUserId = "u1" },
            new() { ReviewerScope = "Application owner", Outcome = SignoffOutcome.Approved, ReviewerUserId = "u2" }
        ];

        Assert.True(ChangeCompletionGateEvaluator.Evaluate(PassingValidation(), signoffs, PassingRehearsal, false).CanComplete);
    }
}
