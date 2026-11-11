using DBTracker.Core.Entities;
using DBTracker.Core.Enums;
using DBTracker.Core.Services;
using Xunit;

namespace DBTracker.UnitTests;

/// <summary>G14: rehearsal and approval gates before Ready, Scheduled and In progress; path guidance merging.</summary>
public class CampaignTransitionGateEvaluatorTests
{
    private static readonly DateTime Now = new(2026, 11, 10, 0, 0, 0, DateTimeKind.Utc);

    private static InstanceRehearsalStatus Passed(string name = "DB-1") =>
        new(name, new RehearsalReadinessResult(RehearsalReadinessStatus.Passed, "ok"));

    private static InstanceRehearsalStatus Missing(string name = "DB-2") =>
        new(name, new RehearsalReadinessResult(RehearsalReadinessStatus.Missing, "No rehearsal has been recorded."));

    private static CampaignApproval Approval(string scope, CampaignApprovalDecision decision, int minutesAgo) =>
        new() { Scope = scope, Decision = decision, ApproverName = "Marcus", DecidedAtUtc = Now.AddMinutes(-minutesAgo) };

    [Theory]
    [InlineData(CampaignState.Ready)]
    [InlineData(CampaignState.Scheduled)]
    [InlineData(CampaignState.InProgress)]
    public void MissingRehearsal_BlocksReadyScheduledAndInProgress(CampaignState to)
    {
        var result = CampaignTransitionGateEvaluator.Evaluate(to, [Passed(), Missing()], [Approval("DBA", CampaignApprovalDecision.Approved, 5)], false);

        Assert.False(result.Allowed);
        Assert.Contains(result.BlockingReasons, r => r.Contains("DB-2") && r.Contains("Missing"));
    }

    [Theory]
    [InlineData(CampaignState.Assessing)]
    [InlineData(CampaignState.Blocked)]
    [InlineData(CampaignState.Cancelled)]
    public void OtherStates_AreNotGatedHere(CampaignState to)
    {
        Assert.True(CampaignTransitionGateEvaluator.Evaluate(to, [Missing()], [], false).Allowed);
    }

    [Fact]
    public void NoLinkedInstances_BlocksReady()
    {
        var result = CampaignTransitionGateEvaluator.Evaluate(CampaignState.Ready, [], [], false);
        Assert.False(result.Allowed);
    }

    [Fact]
    public void ActiveException_WaivesRehearsal_ButNotApproval()
    {
        var ready = CampaignTransitionGateEvaluator.Evaluate(CampaignState.Ready, [Missing()], [], true);
        Assert.True(ready.Allowed);
        Assert.True(ready.UsedException);

        var scheduled = CampaignTransitionGateEvaluator.Evaluate(CampaignState.Scheduled, [Missing()], [], true);
        Assert.False(scheduled.Allowed);
        Assert.Contains(scheduled.BlockingReasons, r => r.Contains("No approval"));
    }

    [Fact]
    public void Ready_DoesNotNeedApproval()
    {
        Assert.True(CampaignTransitionGateEvaluator.Evaluate(CampaignState.Ready, [Passed()], [], false).Allowed);
    }

    [Fact]
    public void Scheduled_NeedsAnApproval()
    {
        Assert.False(CampaignTransitionGateEvaluator.Evaluate(CampaignState.Scheduled, [Passed()], [], false).Allowed);
        Assert.True(CampaignTransitionGateEvaluator.Evaluate(CampaignState.Scheduled, [Passed()], [Approval("DBA", CampaignApprovalDecision.Approved, 1)], false).Allowed);
    }

    [Fact]
    public void LatestRejectionForAScope_Blocks_UntilALaterApprovalForThatScope()
    {
        var approvals = new List<CampaignApproval>
        {
            Approval("DBA technical", CampaignApprovalDecision.Approved, 30),
            Approval("Application owner", CampaignApprovalDecision.Rejected, 10)
        };
        var blocked = CampaignTransitionGateEvaluator.Evaluate(CampaignState.Scheduled, [Passed()], approvals, false);
        Assert.False(blocked.Allowed);
        Assert.Contains(blocked.BlockingReasons, r => r.Contains("Application owner"));

        approvals.Add(Approval("application owner ", CampaignApprovalDecision.Approved, 1));
        Assert.True(CampaignTransitionGateEvaluator.Evaluate(CampaignState.Scheduled, [Passed()], approvals, false).Allowed);
    }

    [Fact]
    public void MergeGuidance_KeepsBothTheEnteredAndTheRuleText()
    {
        Assert.Equal("via 2017", PathSupportClassifier.MergeGuidance("", "via 2017"));
        Assert.Equal("mine", PathSupportClassifier.MergeGuidance("mine", ""));
        Assert.Equal("mine", PathSupportClassifier.MergeGuidance("mine", "MINE"));
        Assert.Equal("mine (matched rule: via 2017)", PathSupportClassifier.MergeGuidance("mine", "via 2017"));
    }
}
