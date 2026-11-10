using DBTracker.Core.Entities;
using DBTracker.Core.Enums;

namespace DBTracker.Core.Services;

public record RetentionCoverageResult(RetentionCoverageStatus Status, string Reasoning);

/// <summary>
/// D17 — compares recorded, currently-available backup-copy evidence against a retention policy
/// (reusing U21/D01's policy pattern rather than a new one). A copy that was merely discovered once,
/// long ago, and is no longer currently verified accessible never counts toward coverage — only
/// copies BackupCopyAvailabilityEvaluator currently calls available can.
/// </summary>
public static class RetentionCoverageComparer
{
    public static RetentionCoverageResult Compare(int? requiredRetentionDays, IReadOnlyCollection<BackupCopyLocation> copies, DateTime nowUtc, int staleAfterDays)
    {
        if (requiredRetentionDays is null)
        {
            return new RetentionCoverageResult(RetentionCoverageStatus.Unknown, "No retention policy recorded to compare against.");
        }

        if (copies.Count == 0)
        {
            return new RetentionCoverageResult(RetentionCoverageStatus.Unknown, "No backup-copy locations recorded.");
        }

        var currentlyAvailable = copies
            .Where(c => BackupCopyAvailabilityEvaluator.Evaluate(c, nowUtc, staleAfterDays).IsCurrentlyVerifiedAvailable)
            .ToList();

        if (currentlyAvailable.Count == 0)
        {
            return new RetentionCoverageResult(RetentionCoverageStatus.Gap,
                "No copy is currently verified accessible — historical discovery of a now-unverified copy does not satisfy the policy.");
        }

        var oldestDiscovered = currentlyAvailable
            .Where(c => c.FirstDiscoveredAtUtc.HasValue)
            .Select(c => c.FirstDiscoveredAtUtc!.Value)
            .Cast<DateTime?>()
            .DefaultIfEmpty(null)
            .Min();

        if (oldestDiscovered is null)
        {
            return new RetentionCoverageResult(RetentionCoverageStatus.Unknown,
                "An accessible copy exists, but its discovery date is unknown — can't confirm how far back its coverage actually reaches.");
        }

        var coveredDays = (nowUtc - oldestDiscovered.Value).TotalDays;
        return coveredDays >= requiredRetentionDays
            ? new RetentionCoverageResult(RetentionCoverageStatus.Compliant, $"Accessible copy evidence spans {coveredDays:N0} day(s), meeting the {requiredRetentionDays}-day policy.")
            : new RetentionCoverageResult(RetentionCoverageStatus.Gap, $"Accessible copy evidence only spans {coveredDays:N0} day(s), short of the {requiredRetentionDays}-day policy.");
    }
}
