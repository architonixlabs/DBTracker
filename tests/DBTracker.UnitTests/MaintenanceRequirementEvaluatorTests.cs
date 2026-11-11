using DBTracker.Core.Entities;
using DBTracker.Core.Enums;
using DBTracker.Core.Services;
using Xunit;

namespace DBTracker.UnitTests;

public class MaintenanceRequirementEvaluatorTests
{
    private static readonly DateTime Now = new(2026, 11, 11, 0, 0, 0, DateTimeKind.Utc);

    private static MaintenanceCheckRequirement Requirement(MaintenanceCheckType type = MaintenanceCheckType.IntegrityCheck, string node = "", int hours = 168) =>
        new() { CheckType = type, NodeOrDeploymentLabel = node, ExpectedIntervalHours = hours };

    private static MaintenanceEvidenceRecord Result(MaintenanceCheckType type = MaintenanceCheckType.IntegrityCheck, MaintenanceOutcome outcome = MaintenanceOutcome.Passed,
        int hoursAgo = 24, string node = "", int? interval = null) =>
        new() { CheckType = type, Outcome = outcome, RanAtUtc = Now.AddHours(-hoursAgo), NodeOrDeploymentLabel = node, ExpectedIntervalHours = interval };

    [Fact]
    public void RequiredCheck_NeverRecorded_IsMissing()
    {
        var status = MaintenanceRequirementEvaluator.Evaluate([Requirement()], [], Now).Single();

        Assert.Equal(MaintenanceEvidenceStatus.Missing, status.Status);
        Assert.Contains("never been recorded", status.Explanation);
    }

    [Fact]
    public void ResultOfAnotherType_DoesNotSatisfyTheRequirement()
    {
        var status = MaintenanceRequirementEvaluator.Evaluate([Requirement()], [Result(MaintenanceCheckType.FailoverDrill)], Now).Single();

        Assert.Equal(MaintenanceEvidenceStatus.Missing, status.Status);
    }

    [Fact]
    public void RecentPass_IsCurrent_UsingTheRequirementInterval()
    {
        var status = MaintenanceRequirementEvaluator.Evaluate([Requirement(hours: 48)], [Result(hoursAgo: 24)], Now).Single();

        Assert.Equal(MaintenanceEvidenceStatus.Current, status.Status);
    }

    [Fact]
    public void OldPass_IsStale()
    {
        var status = MaintenanceRequirementEvaluator.Evaluate([Requirement(hours: 48)], [Result(hoursAgo: 72)], Now).Single();

        Assert.Equal(MaintenanceEvidenceStatus.Stale, status.Status);
    }

    [Fact]
    public void LatestResultFailed_IsFailed_EvenAfterAnEarlierPass()
    {
        var status = MaintenanceRequirementEvaluator.Evaluate([Requirement()],
            [Result(hoursAgo: 50), Result(outcome: MaintenanceOutcome.Failed, hoursAgo: 10)], Now).Single();

        Assert.Equal(MaintenanceEvidenceStatus.Failed, status.Status);
    }

    [Fact]
    public void NodeSpecificRequirement_IgnoresOtherNodes()
    {
        var requirements = new[] { Requirement(node: "node-b") };

        var status = MaintenanceRequirementEvaluator.Evaluate(requirements, [Result(node: "node-a")], Now).Single();

        Assert.Equal(MaintenanceEvidenceStatus.Missing, status.Status);
        Assert.Contains("node-b", status.Explanation);
    }

    [Fact]
    public void UnknownOutcome_StaysUnknown()
    {
        var status = MaintenanceRequirementEvaluator.Evaluate([Requirement()], [Result(outcome: MaintenanceOutcome.Unknown)], Now).Single();

        Assert.Equal(MaintenanceEvidenceStatus.Unknown, status.Status);
    }
}
