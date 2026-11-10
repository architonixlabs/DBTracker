using DBTracker.Core.Entities;
using DBTracker.Core.Enums;
using DBTracker.Core.Services;
using Xunit;

namespace DBTracker.UnitTests;

public class ChangeWindowApprovalGateTests
{
    private static ChangeWindowApproval Approval(string scope, CampaignApprovalDecision? decision) =>
        new() { RequiredScope = scope, Decision = decision };

    [Fact]
    public void NoApprovalsRequired_CanProceed()
    {
        var result = ChangeWindowApprovalGate.Evaluate([], hasActiveEmergencyException: false);

        Assert.True(result.CanProceed);
        Assert.False(result.UsedEmergencyException);
    }

    [Fact]
    public void AllApproved_CanProceed()
    {
        var approvals = new[] { Approval("DBA", CampaignApprovalDecision.Approved), Approval("Change manager", CampaignApprovalDecision.Approved) };

        var result = ChangeWindowApprovalGate.Evaluate(approvals, hasActiveEmergencyException: false);

        Assert.True(result.CanProceed);
    }

    [Fact]
    public void OnePendingApproval_Blocks()
    {
        var approvals = new[] { Approval("DBA", CampaignApprovalDecision.Approved), Approval("Change manager", null) };

        var result = ChangeWindowApprovalGate.Evaluate(approvals, hasActiveEmergencyException: false);

        Assert.False(result.CanProceed);
        Assert.Single(result.OutstandingApprovals);
        Assert.Contains("pending", result.OutstandingApprovals[0]);
    }

    [Fact]
    public void ARejectedApproval_Blocks_EvenIfOthersApproved()
    {
        var approvals = new[] { Approval("DBA", CampaignApprovalDecision.Approved), Approval("Security", CampaignApprovalDecision.Rejected) };

        var result = ChangeWindowApprovalGate.Evaluate(approvals, hasActiveEmergencyException: false);

        Assert.False(result.CanProceed);
        Assert.Contains("rejected", result.OutstandingApprovals[0]);
    }

    [Fact]
    public void OutstandingApprovals_WithActiveEmergencyException_MayProceed()
    {
        var approvals = new[] { Approval("Change manager", null) };

        var result = ChangeWindowApprovalGate.Evaluate(approvals, hasActiveEmergencyException: true);

        Assert.True(result.CanProceed);
        Assert.True(result.UsedEmergencyException);
        Assert.Single(result.OutstandingApprovals); // still reported, not silently erased
    }
}
