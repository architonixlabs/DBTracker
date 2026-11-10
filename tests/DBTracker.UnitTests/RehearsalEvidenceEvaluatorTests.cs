using DBTracker.Core.Entities;
using DBTracker.Core.Enums;
using DBTracker.Core.Services;
using Xunit;

namespace DBTracker.UnitTests;

public class RehearsalEvidenceEvaluatorTests
{
    private static readonly DateTime Now = new(2026, 11, 10, 0, 0, 0, DateTimeKind.Utc);

    private static RehearsalRecord Passing(DateTime? performedAtUtc = null) => new()
    {
        PerformedAtUtc = performedAtUtc ?? Now.AddDays(-10),
        BackupRestoreOutcome = EvidenceCheckOutcome.Passed,
        CorrectnessCheckOutcome = EvidenceCheckOutcome.Passed,
        PerformanceCheckOutcome = EvidenceCheckOutcome.Passed,
        PostCutoverWriteDisposition = PostCutoverWriteDisposition.Reconciled,
        PostCutoverWriteReconciliationPlan = "Replay queued writes from the change-data-capture buffer onto the target."
    };

    [Fact]
    public void NoRehearsal_IsMissing_NeverTreatedAsPassing()
    {
        var result = RehearsalEvidenceEvaluator.Evaluate(null, Now);

        Assert.Equal(RehearsalReadinessStatus.Missing, result.Status);
        Assert.True(result.BlocksNormalReadiness);
    }

    [Fact]
    public void PassingRehearsal_WithinValidityWindow_Passes()
    {
        var result = RehearsalEvidenceEvaluator.Evaluate(Passing(), Now, defaultValidityDays: 180);

        Assert.Equal(RehearsalReadinessStatus.Passed, result.Status);
        Assert.False(result.BlocksNormalReadiness);
    }

    [Fact]
    public void RehearsalOlderThanValidityWindow_IsStale_NotPassing()
    {
        var rehearsal = Passing(Now.AddDays(-200));
        var result = RehearsalEvidenceEvaluator.Evaluate(rehearsal, Now, defaultValidityDays: 180);

        Assert.Equal(RehearsalReadinessStatus.Stale, result.Status);
        Assert.True(result.BlocksNormalReadiness);
    }

    [Fact]
    public void PerInstanceValidityOverride_TakesPrecedenceOverDefault()
    {
        var rehearsal = Passing(Now.AddDays(-40));
        rehearsal.ValidityDays = 30; // override makes a 40-day-old rehearsal stale even though the 180-day default would not

        var result = RehearsalEvidenceEvaluator.Evaluate(rehearsal, Now, defaultValidityDays: 180);

        Assert.Equal(RehearsalReadinessStatus.Stale, result.Status);
    }

    [Fact]
    public void FailedBackupRestoreStep_Fails_EvenIfRecent()
    {
        var rehearsal = Passing();
        rehearsal.BackupRestoreOutcome = EvidenceCheckOutcome.Failed;

        var result = RehearsalEvidenceEvaluator.Evaluate(rehearsal, Now);

        Assert.Equal(RehearsalReadinessStatus.Failed, result.Status);
        Assert.Contains("backup-restore", result.Reason, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void FailedCorrectnessCheck_Fails()
    {
        var rehearsal = Passing();
        rehearsal.CorrectnessCheckOutcome = EvidenceCheckOutcome.Failed;

        var result = RehearsalEvidenceEvaluator.Evaluate(rehearsal, Now);

        Assert.Equal(RehearsalReadinessStatus.Failed, result.Status);
    }

    [Fact]
    public void FailedPerformanceCheck_Fails()
    {
        var rehearsal = Passing();
        rehearsal.PerformanceCheckOutcome = EvidenceCheckOutcome.Failed;

        var result = RehearsalEvidenceEvaluator.Evaluate(rehearsal, Now);

        Assert.Equal(RehearsalReadinessStatus.Failed, result.Status);
    }

    [Fact]
    public void NotPerformedPerformanceCheck_DoesNotFail_OnlyExplicitFailureBlocks()
    {
        var rehearsal = Passing();
        rehearsal.PerformanceCheckOutcome = EvidenceCheckOutcome.NotPerformed;

        var result = RehearsalEvidenceEvaluator.Evaluate(rehearsal, Now);

        Assert.Equal(RehearsalReadinessStatus.Passed, result.Status);
    }

    [Fact]
    public void DispositionNotNotApplicable_WithNoReconciliationPlan_Fails()
    {
        var rehearsal = Passing();
        rehearsal.PostCutoverWriteDisposition = PostCutoverWriteDisposition.Lost;
        rehearsal.PostCutoverWriteReconciliationPlan = "";

        var result = RehearsalEvidenceEvaluator.Evaluate(rehearsal, Now);

        Assert.Equal(RehearsalReadinessStatus.Failed, result.Status);
        Assert.Contains("reconciled or accepted as lost", result.Reason);
    }

    [Fact]
    public void NotApplicableDisposition_NeedsNoReconciliationPlan()
    {
        var rehearsal = Passing();
        rehearsal.PostCutoverWriteDisposition = PostCutoverWriteDisposition.NotApplicable;
        rehearsal.PostCutoverWriteReconciliationPlan = "";

        var result = RehearsalEvidenceEvaluator.Evaluate(rehearsal, Now);

        Assert.Equal(RehearsalReadinessStatus.Passed, result.Status);
    }
}
