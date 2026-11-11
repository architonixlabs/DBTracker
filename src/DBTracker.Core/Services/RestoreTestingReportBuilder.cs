using DBTracker.Core.Entities;
using DBTracker.Core.Enums;

namespace DBTracker.Core.Services;

public record RestoreTestingInput(
    string InstanceName, string Company, string Department, InstanceRole Role, EnvironmentTier Environment,
    IReadOnlyCollection<RestoreEvent> RestoreEvents, int? IntervalDaysOverride = null);

public record RestoreTestingRow(
    string InstanceName, string Company, string Department, InstanceRole Role, EnvironmentTier Environment,
    bool RestoreTested, DateTime? LastSuccessfulRestoreUtc, int TotalAttempts,
    RestoreTestRecency Recency, DateTime? NextDueUtc);

/// <summary>Which instances have a proven successful restore and whether it is still within the test window. Untested and overdue instances sort first.
/// Imported restore history is not proof, so it never makes an instance restore-tested.</summary>
public static class RestoreTestingReportBuilder
{
    public static IReadOnlyList<RestoreTestingRow> Build(IEnumerable<RestoreTestingInput> inputs, int defaultIntervalDays = 90, DateTime? nowUtc = null)
    {
        var now = nowUtc ?? DateTime.UtcNow;

        return inputs
            .Select(i =>
            {
                var lastSuccess = i.RestoreEvents
                    .Where(RestoreEvidence.CountsAsVerified)
                    .OrderByDescending(e => e.TimestampUtc)
                    .FirstOrDefault();

                var intervalDays = i.IntervalDaysOverride ?? defaultIntervalDays;
                var recency = RestoreTestRecencyCalculator.Compute(lastSuccess?.TimestampUtc, intervalDays, now);

                return new RestoreTestingRow(
                    i.InstanceName, i.Company, i.Department, i.Role, i.Environment,
                    RestoreTested: lastSuccess is not null,
                    LastSuccessfulRestoreUtc: lastSuccess?.TimestampUtc,
                    TotalAttempts: i.RestoreEvents.Count,
                    Recency: recency.Status,
                    NextDueUtc: recency.NextDueUtc);
            })
            .OrderBy(r => r.RestoreTested)
            .ThenBy(r => r.Recency == RestoreTestRecency.Overdue ? 0 : 1)
            .ThenBy(r => r.InstanceName)
            .ToList();
    }
}
