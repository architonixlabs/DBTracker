using DBTracker.Core.Entities;
using DBTracker.Core.Enums;

namespace DBTracker.Core.Services;

public record InstanceRehearsalStatus(string InstanceName, RehearsalReadinessResult Rehearsal);

public record CampaignTransitionGateResult(bool Allowed, IReadOnlyList<string> BlockingReasons, bool UsedException);

/// <summary>Evidence a campaign needs before it may move forward, on top of the state graph. Ready, Scheduled and In
/// progress need a current, passing rehearsal for every linked instance (an active campaign-scoped exception can waive
/// that). Scheduled and In progress also need approval: at least one scope approved, and no scope whose latest decision is
/// a rejection. Approval can't be waived by an exception. Completed keeps its own gate (CampaignCompletionGateService).</summary>
public static class CampaignTransitionGateEvaluator
{
    public static bool NeedsRehearsal(CampaignState toState) =>
        toState is CampaignState.Ready or CampaignState.Scheduled or CampaignState.InProgress;

    public static bool NeedsApproval(CampaignState toState) =>
        toState is CampaignState.Scheduled or CampaignState.InProgress;

    public static CampaignTransitionGateResult Evaluate(
        CampaignState toState,
        IReadOnlyList<InstanceRehearsalStatus> rehearsals,
        IReadOnlyList<CampaignApproval> approvals,
        bool hasActiveException)
    {
        var reasons = new List<string>();
        var usedException = false;

        if (NeedsRehearsal(toState))
        {
            var rehearsalReasons = new List<string>();
            if (rehearsals.Count == 0)
            {
                rehearsalReasons.Add("No instances are linked to this campaign, so there is no rehearsal evidence.");
            }
            foreach (var r in rehearsals.Where(r => r.Rehearsal.BlocksNormalReadiness))
            {
                rehearsalReasons.Add($"{r.InstanceName}: rehearsal {r.Rehearsal.Status}. {r.Rehearsal.Reason}");
            }

            if (rehearsalReasons.Count > 0 && hasActiveException && rehearsals.Count > 0)
            {
                usedException = true;
            }
            else
            {
                reasons.AddRange(rehearsalReasons);
            }
        }

        if (NeedsApproval(toState))
        {
            var latestPerScope = approvals
                .GroupBy(a => (a.Scope ?? "").Trim(), StringComparer.OrdinalIgnoreCase)
                .Select(g => g.OrderByDescending(a => a.DecidedAtUtc).ThenByDescending(a => a.Id).First())
                .ToList();

            var rejected = latestPerScope.Where(a => a.Decision == CampaignApprovalDecision.Rejected).ToList();
            foreach (var r in rejected)
            {
                reasons.Add($"The latest \"{r.Scope}\" decision is a rejection by {r.ApproverName}. Record a new approval for that scope first.");
            }

            if (!latestPerScope.Any(a => a.Decision == CampaignApprovalDecision.Approved))
            {
                reasons.Add("No approval is recorded yet. Record at least one approval before scheduling.");
            }
        }

        return new CampaignTransitionGateResult(reasons.Count == 0, reasons, usedException && reasons.Count == 0);
    }
}
