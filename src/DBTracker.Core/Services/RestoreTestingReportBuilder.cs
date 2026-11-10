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

/// <summary>
/// Which instances have ever had a *successful* restore proven, not just backups taken, and (U12)
/// whether that proof is still within its test-recency window. Serves the Auditor persona — an
/// untested or expired-coverage backup is a known audit red flag. Untested/overdue instances sort
/// first. Pure (no I/O).
/// </summary>
public static class RestoreTestingReportBuilder
{
    public static IReadOnlyList<RestoreTestingRow> Build(IEnumerable<RestoreTestingInput> inputs, int defaultIntervalDays = 90, DateTime? nowUtc = null)
    {
        var now = nowUtc ?? DateTime.UtcNow;

        return inputs
            .Select(i =>
            {
                var lastSuccess = i.RestoreEvents
                    .Where(e => e.Outcome == RestoreOutcome.Success)
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
