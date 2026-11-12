namespace DBTracker.Core.Services;

/// <summary>What the last support-runway notice for an instance was about. Kind is one of the RunwayNoticeKinds values, or
/// null when nothing has been sent (or the row predates kinds).</summary>
public record RunwayNoticeState(int? ThresholdDays, DateTime? MilestoneUtc, string? Kind)
{
    public static readonly RunwayNoticeState None = new(null, null, null);
}

/// <summary>Whether to send a notice now, and the state to store afterwards.</summary>
public record RunwayNoticeDecision(bool Notify, RunwayNoticeState NewState, bool StateChanged);

public static class RunwayNoticeKinds
{
    public const string Deadline = "Deadline";
    public const string Unknown = "Unknown";
    public const string NeedsReview = "NeedsReview";
}

/// <summary>Dedupe for threshold-based support-runway notices: a notice fires again only when a more urgent threshold is crossed. Overdue (days &lt;= 0) is always more urgent, so the due-now notice still fires after the 30-day one.</summary>
public static class SupportRunwayNoticeEvaluator
{
    /// <summary>The full notice decision for one sweep. Dedupe is per milestone: when the milestone date changes (a vendor
    /// date revision, a contract ending, or the next phase starting), earlier notices no longer count and the thresholds
    /// start again. Unknown and Needs review each send one notice when the instance enters that state, so missing facts
    /// are never silently skipped.</summary>
    public static RunwayNoticeDecision Decide(SupportRunwayResult runway, IReadOnlyCollection<int> thresholds, RunwayNoticeState last)
    {
        if (runway.IsUnknown)
        {
            var kind = runway.NeedsReview ? RunwayNoticeKinds.NeedsReview : RunwayNoticeKinds.Unknown;
            if (last.Kind == kind)
            {
                return new RunwayNoticeDecision(false, last, false);
            }
            return new RunwayNoticeDecision(true, new RunwayNoticeState(null, null, kind), true);
        }

        var effective = last;
        var reset = false;
        if (last.Kind is RunwayNoticeKinds.Unknown or RunwayNoticeKinds.NeedsReview)
        {
            reset = true;
        }
        else if (last.MilestoneUtc is { } notifiedMilestone && runway.NextMilestoneUtc is { } current && notifiedMilestone != current)
        {
            reset = true;
        }

        if (reset)
        {
            effective = RunwayNoticeState.None;
        }

        var crossed = ThresholdCrossed(runway.DaysToNextMilestone, thresholds);
        if (crossed is null || !ShouldNotify(runway.DaysToNextMilestone, thresholds, effective.ThresholdDays))
        {
            return new RunwayNoticeDecision(false, effective, reset);
        }

        return new RunwayNoticeDecision(true, new RunwayNoticeState(crossed, runway.NextMilestoneUtc, RunwayNoticeKinds.Deadline), true);
    }

    /// <summary>The most urgent configured threshold (days-before-deadline) that has been crossed, or null if none has.</summary>
    public static int? ThresholdCrossed(int? daysToMilestone, IReadOnlyCollection<int> thresholds)
    {
        if (daysToMilestone is null)
        {
            return null;
        }

        var all = thresholds.Append(0).Distinct().OrderBy(t => t).ToList();
        var crossed = all.Where(t => daysToMilestone <= t).ToList();
        return crossed.Count == 0 ? null : crossed.Min();
    }

    public static bool ShouldNotify(int? daysToMilestone, IReadOnlyCollection<int> thresholds, int? lastNotifiedThresholdDays)
    {
        var crossed = ThresholdCrossed(daysToMilestone, thresholds);
        if (crossed is null)
        {
            return false;
        }

        return lastNotifiedThresholdDays is null || crossed < lastNotifiedThresholdDays;
    }

    /// <summary>The audit action that records an Unknown notice for an instance with no version identity. With no identity row
    /// to hold dedupe state, the audit trail is the record that the one notice was sent.</summary>
    public const string NoIdentityNoticeAuditAction = "SupportRunwayUnknownNoticeRaised";

    /// <summary>The dedupe state to compare against. An identity's own state wins once it has any; until then an Unknown notice
    /// sent before the identity existed still counts, so creating an unresolved identity doesn't send a second Unknown notice.</summary>
    public static RunwayNoticeState LastStateFor(Entities.VersionIdentity? identity, bool unknownNoticeSentWithoutIdentity)
    {
        var fallback = unknownNoticeSentWithoutIdentity ? new RunwayNoticeState(null, null, RunwayNoticeKinds.Unknown) : RunwayNoticeState.None;
        if (identity is null || (string.IsNullOrEmpty(identity.LastRunwayNoticeKind) && identity.LastRunwayNoticeThresholdDays is null))
        {
            return fallback;
        }

        return new RunwayNoticeState(identity.LastRunwayNoticeThresholdDays, identity.LastRunwayNoticeMilestoneUtc,
            string.IsNullOrEmpty(identity.LastRunwayNoticeKind) ? null : identity.LastRunwayNoticeKind);
    }

    public static IReadOnlyList<int> ParseThresholds(string? configured)
    {
        if (string.IsNullOrWhiteSpace(configured))
        {
            return [365, 180, 90, 30];
        }

        var parsed = configured.Split(',', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries)
            .Select(s => int.TryParse(s, out var n) ? n : (int?)null)
            .Where(n => n is > 0)
            .Select(n => n!.Value)
            .Distinct()
            .ToList();

        return parsed.Count == 0 ? [365, 180, 90, 30] : parsed;
    }
}
