using DBTracker.Core.Enums;

namespace DBTracker.Core.Services;

/// <summary>Allowed status changes for a change window. Completed and RolledBack are reachable only from InProgress, so a
/// finished change always passed the approval gate (or an emergency exception) on its way in.</summary>
/// <remarks>Planned to InProgress exists for emergencies; the approval gate still refuses it without an active exception.</remarks>
public static class ChangeWindowStatusTransitionValidator
{
    private static readonly Dictionary<ChangeWindowStatus, ChangeWindowStatus[]> Allowed = new()
    {
        [ChangeWindowStatus.Planned] = [ChangeWindowStatus.Approved, ChangeWindowStatus.InProgress, ChangeWindowStatus.Cancelled],
        [ChangeWindowStatus.Approved] = [ChangeWindowStatus.InProgress, ChangeWindowStatus.Planned, ChangeWindowStatus.Cancelled],
        [ChangeWindowStatus.InProgress] = [ChangeWindowStatus.Completed, ChangeWindowStatus.RolledBack],
        [ChangeWindowStatus.Completed] = [],
        [ChangeWindowStatus.RolledBack] = [],
        [ChangeWindowStatus.Cancelled] = [],
    };

    public static IReadOnlyList<ChangeWindowStatus> AllowedNextStatuses(ChangeWindowStatus from) =>
        Allowed.TryGetValue(from, out var targets) ? targets : [];

    public static bool IsValidTransition(ChangeWindowStatus from, ChangeWindowStatus to) =>
        from != to && AllowedNextStatuses(from).Contains(to);

    public static string DescribeInvalidReason(ChangeWindowStatus from, ChangeWindowStatus to)
    {
        if (from == to)
        {
            return $"Already {from}.";
        }

        var targets = AllowedNextStatuses(from);
        return targets.Count == 0
            ? $"{from} is final. Plan a new change window instead."
            : $"A {from} window can only move to {string.Join(", ", targets)}.";
    }
}
