using DBTracker.Core.Entities;
using DBTracker.Core.Enums;

namespace DBTracker.Core.Services;

public record RestoreTestingInput(string InstanceName, string Company, string Department, InstanceRole Role, EnvironmentTier Environment, IReadOnlyCollection<RestoreEvent> RestoreEvents);
public record RestoreTestingRow(string InstanceName, string Company, string Department, InstanceRole Role, EnvironmentTier Environment, bool RestoreTested, DateTime? LastSuccessfulRestoreUtc, int TotalAttempts);

/// <summary>
/// Which instances have ever had a *successful* restore proven, not just backups taken. Serves the
/// Auditor persona — an untested backup is a known audit red flag. Untested instances sort first.
/// Pure (no I/O).
/// </summary>
public static class RestoreTestingReportBuilder
{
    public static IReadOnlyList<RestoreTestingRow> Build(IEnumerable<RestoreTestingInput> inputs) =>
        inputs
            .Select(i =>
            {
                var lastSuccess = i.RestoreEvents
                    .Where(e => e.Outcome == RestoreOutcome.Success)
                    .OrderByDescending(e => e.TimestampUtc)
                    .FirstOrDefault();

                return new RestoreTestingRow(
                    i.InstanceName, i.Company, i.Department, i.Role, i.Environment,
                    RestoreTested: lastSuccess is not null,
                    LastSuccessfulRestoreUtc: lastSuccess?.TimestampUtc,
                    TotalAttempts: i.RestoreEvents.Count);
            })
            .OrderBy(r => r.RestoreTested)
            .ThenBy(r => r.InstanceName)
            .ToList();
}
