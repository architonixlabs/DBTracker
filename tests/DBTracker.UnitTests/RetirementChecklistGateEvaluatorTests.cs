using DBTracker.Core.Entities;
using DBTracker.Core.Enums;
using DBTracker.Core.Services;
using Xunit;

namespace DBTracker.UnitTests;

public class RetirementChecklistGateEvaluatorTests
{
    private static readonly DateTime Now = new(2026, 11, 10, 0, 0, 0, DateTimeKind.Utc);

    private static RetirementChecklist FullyPassingChecklist() => new()
    {
        LegalHoldReviewed = true,
        AppOwnerSignoffBy = "Dev Patel",
        AppOwnerSignoffAtUtc = Now,
        PlatformSignoffBy = "Sam Okoro",
        PlatformSignoffAtUtc = Now,
        CostContractReviewed = true
    };

    private static readonly InstanceDependency[] AllResolvedDependencies =
    [
        new() { Direction = DependencyDirection.Downstream, Kind = DependencyKind.Consumer, Name = "BillingApp", IsResolved = true },
        new() { Direction = DependencyDirection.Upstream, Kind = DependencyKind.Job, Name = "nightly-etl", IsResolved = true }
    ];

    private static RetirementChecklistEvaluation EvaluateAllPassing(IReadOnlyCollection<InstanceDependency>? dependencies = null) =>
        RetirementChecklistGateEvaluator.Evaluate(
            FullyPassingChecklist(),
            dependencies ?? AllResolvedDependencies,
            chainCoverageStatus: ChainCoverageStatus.Complete,
            hasAccessibleVerifiedCopy: true,
            hasAnyCopyLocationsRecorded: true,
            certificateAccessVerified: null,
            rollbackRetentionDeadlineUtc: null,
            nowUtc: Now);

    [Fact]
    public void BrandNewChecklist_NothingRecorded_IsNotSatisfied()
    {
        var evaluation = RetirementChecklistGateEvaluator.Evaluate(
            new RetirementChecklist(), [], chainCoverageStatus: null, hasAccessibleVerifiedCopy: false,
            hasAnyCopyLocationsRecorded: false, certificateAccessVerified: null, rollbackRetentionDeadlineUtc: null, nowUtc: Now);

        Assert.False(evaluation.AllGatesSatisfied);
        Assert.NotEmpty(evaluation.BlockingReasons);
    }

    [Fact]
    public void EveryGateSatisfied_RollbackNotApplicable_LegalHoldAndSignoffsAndCostDone_IsSatisfied()
    {
        var evaluation = EvaluateAllPassing();

        Assert.True(evaluation.AllGatesSatisfied);
        Assert.Empty(evaluation.BlockingReasons);
        Assert.All(evaluation.Gates, g => Assert.True(g.Status is RetirementGateStatus.Passed or RetirementGateStatus.NotApplicable));
    }

    [Fact]
    public void UnresolvedDownstreamConsumer_Blocks_AndNamesIt()
    {
        var dependency = new InstanceDependency { Direction = DependencyDirection.Downstream, Kind = DependencyKind.Consumer, Name = "BillingApp", IsResolved = false };
        var evaluation = EvaluateAllPassing([dependency]);

        Assert.False(evaluation.AllGatesSatisfied);
        var gate = Assert.Single(evaluation.Gates, g => g.GateKey == "ConsumersResolved");
        Assert.Equal(RetirementGateStatus.Blocked, gate.Status);
        Assert.Contains("BillingApp", gate.Detail);
    }

    [Fact]
    public void ResolvedDownstreamConsumer_Passes()
    {
        var dependency = new InstanceDependency { Direction = DependencyDirection.Downstream, Kind = DependencyKind.Consumer, Name = "BillingApp", IsResolved = true };
        var evaluation = EvaluateAllPassing([dependency]);

        var gate = Assert.Single(evaluation.Gates, g => g.GateKey == "ConsumersResolved");
        Assert.Equal(RetirementGateStatus.Passed, gate.Status);
    }

    [Fact]
    public void NoDependenciesRecordedAtAll_IsUnknown_NeverPassed()
    {
        var evaluation = EvaluateAllPassing([]);

        var consumersGate = Assert.Single(evaluation.Gates, g => g.GateKey == "ConsumersResolved");
        var jobsGate = Assert.Single(evaluation.Gates, g => g.GateKey == "JobsReplicasResolved");
        Assert.Equal(RetirementGateStatus.Unknown, consumersGate.Status);
        Assert.Equal(RetirementGateStatus.Unknown, jobsGate.Status);
        Assert.False(evaluation.AllGatesSatisfied);
    }

    [Theory]
    [InlineData(DependencyKind.Job)]
    [InlineData(DependencyKind.Replica)]
    [InlineData(DependencyKind.LinkedService)]
    [InlineData(DependencyKind.BackupTooling)]
    public void UnresolvedJobOrReplicaDependency_Blocks(DependencyKind kind)
    {
        var dependency = new InstanceDependency { Direction = DependencyDirection.Upstream, Kind = kind, Name = "nightly-etl", IsResolved = false };
        var evaluation = EvaluateAllPassing([dependency]);

        var gate = Assert.Single(evaluation.Gates, g => g.GateKey == "JobsReplicasResolved");
        Assert.Equal(RetirementGateStatus.Blocked, gate.Status);
    }

    [Fact]
    public void ChainCoverageUnknown_BlocksFinalEvidenceGateAsUnknown_NeverGreen()
    {
        var evaluation = RetirementChecklistGateEvaluator.Evaluate(
            FullyPassingChecklist(), [], chainCoverageStatus: ChainCoverageStatus.Unknown, hasAccessibleVerifiedCopy: true,
            hasAnyCopyLocationsRecorded: true, certificateAccessVerified: null, rollbackRetentionDeadlineUtc: null, nowUtc: Now);

        var gate = Assert.Single(evaluation.Gates, g => g.GateKey == "FinalRecoverableEvidence");
        Assert.Equal(RetirementGateStatus.Unknown, gate.Status);
        Assert.False(evaluation.AllGatesSatisfied);
    }

    [Fact]
    public void ChainCoverageIncomplete_BlocksFinalEvidenceGate()
    {
        var evaluation = RetirementChecklistGateEvaluator.Evaluate(
            FullyPassingChecklist(), [], chainCoverageStatus: ChainCoverageStatus.Incomplete, hasAccessibleVerifiedCopy: true,
            hasAnyCopyLocationsRecorded: true, certificateAccessVerified: null, rollbackRetentionDeadlineUtc: null, nowUtc: Now);

        var gate = Assert.Single(evaluation.Gates, g => g.GateKey == "FinalRecoverableEvidence");
        Assert.Equal(RetirementGateStatus.Blocked, gate.Status);
    }

    [Fact]
    public void NoCopyLocationsRecorded_FinalEvidenceGateIsUnknown()
    {
        var evaluation = RetirementChecklistGateEvaluator.Evaluate(
            FullyPassingChecklist(), [], chainCoverageStatus: ChainCoverageStatus.Complete, hasAccessibleVerifiedCopy: false,
            hasAnyCopyLocationsRecorded: false, certificateAccessVerified: null, rollbackRetentionDeadlineUtc: null, nowUtc: Now);

        var gate = Assert.Single(evaluation.Gates, g => g.GateKey == "FinalRecoverableEvidence");
        Assert.Equal(RetirementGateStatus.Unknown, gate.Status);
    }

    [Fact]
    public void CopyLocationsRecordedButNoneAccessible_FinalEvidenceGateBlocks()
    {
        var evaluation = RetirementChecklistGateEvaluator.Evaluate(
            FullyPassingChecklist(), [], chainCoverageStatus: ChainCoverageStatus.Complete, hasAccessibleVerifiedCopy: false,
            hasAnyCopyLocationsRecorded: true, certificateAccessVerified: null, rollbackRetentionDeadlineUtc: null, nowUtc: Now);

        var gate = Assert.Single(evaluation.Gates, g => g.GateKey == "FinalRecoverableEvidence");
        Assert.Equal(RetirementGateStatus.Blocked, gate.Status);
    }

    [Fact]
    public void CertificateAccessNotVerified_BlocksFinalEvidenceGate_EvenWithGoodChainAndCopy()
    {
        var evaluation = RetirementChecklistGateEvaluator.Evaluate(
            FullyPassingChecklist(), [], chainCoverageStatus: ChainCoverageStatus.Complete, hasAccessibleVerifiedCopy: true,
            hasAnyCopyLocationsRecorded: true, certificateAccessVerified: false, rollbackRetentionDeadlineUtc: null, nowUtc: Now);

        var gate = Assert.Single(evaluation.Gates, g => g.GateKey == "FinalRecoverableEvidence");
        Assert.Equal(RetirementGateStatus.Blocked, gate.Status);
    }

    [Fact]
    public void NoRollbackDeadlineRecorded_IsNotApplicable_NotBlocking()
    {
        var evaluation = EvaluateAllPassing();
        var gate = Assert.Single(evaluation.Gates, g => g.GateKey == "RollbackWindowClosed");

        Assert.Equal(RetirementGateStatus.NotApplicable, gate.Status);
    }

    [Fact]
    public void RollbackDeadlineInTheFuture_Blocks()
    {
        var evaluation = RetirementChecklistGateEvaluator.Evaluate(
            FullyPassingChecklist(), [], chainCoverageStatus: ChainCoverageStatus.Complete, hasAccessibleVerifiedCopy: true,
            hasAnyCopyLocationsRecorded: true, certificateAccessVerified: null, rollbackRetentionDeadlineUtc: Now.AddDays(5), nowUtc: Now);

        var gate = Assert.Single(evaluation.Gates, g => g.GateKey == "RollbackWindowClosed");
        Assert.Equal(RetirementGateStatus.Blocked, gate.Status);
        Assert.False(evaluation.AllGatesSatisfied);
    }

    [Fact]
    public void RollbackDeadlineInThePast_Passes()
    {
        var evaluation = RetirementChecklistGateEvaluator.Evaluate(
            FullyPassingChecklist(), [], chainCoverageStatus: ChainCoverageStatus.Complete, hasAccessibleVerifiedCopy: true,
            hasAnyCopyLocationsRecorded: true, certificateAccessVerified: null, rollbackRetentionDeadlineUtc: Now.AddDays(-1), nowUtc: Now);

        var gate = Assert.Single(evaluation.Gates, g => g.GateKey == "RollbackWindowClosed");
        Assert.Equal(RetirementGateStatus.Passed, gate.Status);
    }

    [Fact]
    public void LegalHoldNotReviewed_IsUnknown_NotBlockedNotPassed()
    {
        var checklist = FullyPassingChecklist();
        checklist.LegalHoldReviewed = false;
        var evaluation = RetirementChecklistGateEvaluator.Evaluate(
            checklist, [], ChainCoverageStatus.Complete, true, true, null, null, Now);

        var gate = Assert.Single(evaluation.Gates, g => g.GateKey == "LegalHoldReviewed");
        Assert.Equal(RetirementGateStatus.Unknown, gate.Status);
        Assert.False(evaluation.AllGatesSatisfied);
    }

    [Fact]
    public void MissingEitherSignoff_IsUnknown()
    {
        var checklist = FullyPassingChecklist();
        checklist.PlatformSignoffAtUtc = null;
        var evaluation = RetirementChecklistGateEvaluator.Evaluate(
            checklist, [], ChainCoverageStatus.Complete, true, true, null, null, Now);

        var gate = Assert.Single(evaluation.Gates, g => g.GateKey == "Signoffs");
        Assert.Equal(RetirementGateStatus.Unknown, gate.Status);
        Assert.Contains("platform", gate.Detail);
    }

    [Fact]
    public void CostContractNotReviewed_IsUnknown()
    {
        var checklist = FullyPassingChecklist();
        checklist.CostContractReviewed = false;
        var evaluation = RetirementChecklistGateEvaluator.Evaluate(
            checklist, [], ChainCoverageStatus.Complete, true, true, null, null, Now);

        var gate = Assert.Single(evaluation.Gates, g => g.GateKey == "CostContractReview");
        Assert.Equal(RetirementGateStatus.Unknown, gate.Status);
    }

    [Fact]
    public void BlockingReasons_IncludeBothBlockedAndUnknownGates_NotJustBlocked()
    {
        var checklist = new RetirementChecklist(); // nothing reviewed/signed yet — mostly Unknown
        var evaluation = RetirementChecklistGateEvaluator.Evaluate(
            checklist, [], null, false, false, null, null, Now);

        Assert.False(evaluation.AllGatesSatisfied);
        // Every Unknown/Blocked gate must show up as a reason — nothing is silently dropped.
        Assert.True(evaluation.BlockingReasons.Count >= evaluation.Gates.Count(g => g.Status is RetirementGateStatus.Blocked or RetirementGateStatus.Unknown));
    }
}
