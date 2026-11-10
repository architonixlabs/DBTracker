using DBTracker.Core.Entities;
using DBTracker.Core.Enums;

namespace DBTracker.Core.Services;

public record ChangeWindowApprovalGateResult(bool CanProceed, IReadOnlyList<string> OutstandingApprovals, bool UsedEmergencyException);

/// <summary>
/// Pure (no I/O) gate deciding whether a D12 ChangeWindow may advance past Planned. Every recorded
/// ChangeWindowApproval must be Approved — a single Rejected or still-pending approval blocks, unless
/// an active, linked D14 emergency exception is supplied, in which case the window may proceed with
/// that explicitly recorded as an urgent exception rather than pretending every gate passed.
/// </summary>
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
