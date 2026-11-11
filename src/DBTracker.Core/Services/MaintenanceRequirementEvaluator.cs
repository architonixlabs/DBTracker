using DBTracker.Core.Entities;
using DBTracker.Core.Enums;

namespace DBTracker.Core.Services;

public record MaintenanceRequirementStatus(
    MaintenanceCheckRequirement Requirement,
    MaintenanceEvidenceStatus Status,
    MaintenanceEvidenceRecord? LatestResult,
    string Explanation);

/// <summary>Compares required checks with the results on record. A requirement with no matching result is Missing, the latest result's
/// failure wins, and a passing result older than the expected interval is Stale. Kept apart from backup health in both directions.</summary>
public static class MaintenanceRequirementEvaluator
{
    public static IReadOnlyList<MaintenanceRequirementStatus> Evaluate(
        IReadOnlyCollection<MaintenanceCheckRequirement> requirements,
        IReadOnlyCollection<MaintenanceEvidenceRecord> results,
        DateTime nowUtc) =>
        requirements
            .OrderBy(r => r.CheckType).ThenBy(r => r.NodeOrDeploymentLabel)
            .Select(r => EvaluateOne(r, results, nowUtc))
            .ToList();

    private static MaintenanceRequirementStatus EvaluateOne(MaintenanceCheckRequirement requirement, IReadOnlyCollection<MaintenanceEvidenceRecord> results, DateTime nowUtc)
    {
        var scope = string.IsNullOrWhiteSpace(requirement.NodeOrDeploymentLabel) ? "" : $" on {requirement.NodeOrDeploymentLabel.Trim()}";
        var latest = results
            .Where(r => r.CheckType == requirement.CheckType && r.RanAtUtc is not null && r.RanAtUtc <= nowUtc)
            .Where(r => string.IsNullOrWhiteSpace(requirement.NodeOrDeploymentLabel) ||
                        string.Equals(r.NodeOrDeploymentLabel.Trim(), requirement.NodeOrDeploymentLabel.Trim(), StringComparison.OrdinalIgnoreCase) ||
                        string.Equals(r.RelatedDependency?.Name?.Trim(), requirement.NodeOrDeploymentLabel.Trim(), StringComparison.OrdinalIgnoreCase))
            .OrderByDescending(r => r.RanAtUtc)
            .FirstOrDefault();

        if (latest is null)
        {
            return new(requirement, MaintenanceEvidenceStatus.Missing, null,
                $"Required {requirement.CheckType}{scope} has never been recorded.");
        }

        var interval = Math.Max(1, latest.ExpectedIntervalHours ?? requirement.ExpectedIntervalHours);
        var ranAt = latest.RanAtUtc!.Value;

        if (latest.Outcome == MaintenanceOutcome.Failed)
        {
            return new(requirement, MaintenanceEvidenceStatus.Failed, latest, $"The latest {requirement.CheckType}{scope} ({ranAt:yyyy-MM-dd HH:mm} UTC) failed.");
        }
        if (latest.Outcome == MaintenanceOutcome.Unknown)
        {
            return new(requirement, MaintenanceEvidenceStatus.Unknown, latest, $"The latest {requirement.CheckType}{scope} ({ranAt:yyyy-MM-dd HH:mm} UTC) has no recorded outcome.");
        }
        if (ranAt.AddHours(interval) <= nowUtc)
        {
            return new(requirement, MaintenanceEvidenceStatus.Stale, latest,
                $"The latest passing {requirement.CheckType}{scope} ran {ranAt:yyyy-MM-dd HH:mm} UTC, more than {interval} hours ago.");
        }
        return new(requirement, MaintenanceEvidenceStatus.Current, latest,
            $"Passed {ranAt:yyyy-MM-dd HH:mm} UTC, within the expected {interval} hours. This is a recorded result, not live monitoring.");
    }
}
