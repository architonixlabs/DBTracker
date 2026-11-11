using DBTracker.Core.Enums;

namespace DBTracker.Core.Services;

/// <summary>Allowed state transitions for a migration campaign: the forward flow, Blocked back to Assessing, Cancelled from any unfinished state, and Rolled back re-entering only via Assessing.</summary>
/// <remarks>Never reads or writes risk or readiness records, so a state change can't imply anything about them.</remarks>
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
