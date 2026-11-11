using DBTracker.Core.Entities;
using DBTracker.Core.Enums;

namespace DBTracker.Core.Services;

public record ChangeWindowApprovalGateResult(bool CanProceed, IReadOnlyList<string> OutstandingApprovals, bool UsedEmergencyException);

/// <summary>Whether a change window may advance past Planned. Every approval must be Approved, unless an active linked emergency exception is supplied, which is then recorded as such.</summary>
public static class ChangeWindowApprovalGate
{
    public static ChangeWindowApprovalGateResult Evaluate(IReadOnlyList<ChangeWindowApproval> approvals, bool hasActiveEmergencyException)
    {
        var outstanding = approvals
            .Where(a => a.Decision != CampaignApprovalDecision.Approved)
            .Select(a => a.Decision == CampaignApprovalDecision.Rejected
                ? $"{a.RequiredScope}: rejected."
                : $"{a.RequiredScope}: approval still pending.")
            .ToList();

        if (outstanding.Count == 0)
        {
            return new ChangeWindowApprovalGateResult(true, outstanding, false);
        }

        if (hasActiveEmergencyException)
        {
            return new ChangeWindowApprovalGateResult(true, outstanding, true);
        }

        return new ChangeWindowApprovalGateResult(false, outstanding, false);
    }
}
