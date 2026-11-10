using DBTracker.Core.Enums;

namespace DBTracker.Core.Services;

/// <summary>
/// Pure state-machine rules for a D10 MigrationCampaign. Encodes the normal forward flow
/// (Proposed → Assessing → Ready → Scheduled → In progress → Validating → Completed) plus the
/// documented exception exits: Blocked is reachable from most active states and returns only to
/// Assessing; Cancelled is reachable from every state that hasn't already finished; Rolled back is
/// reachable from In progress/Validating/Completed (a problem can surface even after sign-off) and
/// only re-enters the flow via Assessing, never jumps straight back to Ready/Scheduled. Cancelled and
/// a final Completed (no later rollback) are terminal. This file decides nothing about risk or
/// readiness — CampaignService.TransitionAsync calls this and this alone before writing a transition;
/// it never reads or writes PathAssessment/ReadinessChecklist/SupportRunway, so a state change here
/// cannot itself change or imply anything about risk/readiness elsewhere in the app.
/// </summary>
public static class CampaignStateTransitionValidator
{
    private static readonly Dictionary<CampaignState, CampaignState[]> Allowed = new()
    {
        [CampaignState.Proposed] = [CampaignState.Assessing, CampaignState.Cancelled],
        [CampaignState.Assessing] = [CampaignState.Blocked, CampaignState.Ready, CampaignState.Cancelled],
        [CampaignState.Blocked] = [CampaignState.Assessing, CampaignState.Cancelled],
        [CampaignState.Ready] = [CampaignState.Scheduled, CampaignState.Blocked, CampaignState.Cancelled],
        [CampaignState.Scheduled] = [CampaignState.InProgress, CampaignState.Blocked, CampaignState.Cancelled],
        [CampaignState.InProgress] = [CampaignState.Validating, CampaignState.Blocked, CampaignState.RolledBack],
        [CampaignState.Validating] = [CampaignState.Completed, CampaignState.Blocked, CampaignState.RolledBack],
        [CampaignState.Completed] = [CampaignState.RolledBack],
        [CampaignState.RolledBack] = [CampaignState.Assessing],
        [CampaignState.Cancelled] = [],
    };

    public static IReadOnlyList<CampaignState> AllowedNextStates(CampaignState from) =>
        Allowed.TryGetValue(from, out var targets) ? targets : [];

    public static bool IsValidTransition(CampaignState from, CampaignState to) =>
        from != to && Allowed.TryGetValue(from, out var targets) && targets.Contains(to);

    public static string DescribeInvalidReason(CampaignState from, CampaignState to)
    {
        if (from == to)
        {
            return $"Already in {from}.";
        }

        var targets = AllowedNextStates(from);
        if (targets.Count == 0)
        {
            return $"{from} is a terminal state with no further transitions.";
        }

        return $"{from} cannot transition directly to {to}. Valid next state(s): {string.Join(", ", targets)}.";
    }
}
