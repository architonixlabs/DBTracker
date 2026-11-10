using DBTracker.Core.Enums;

namespace DBTracker.Core.Services;

public record RestoreTestRecencyResult(RestoreTestRecency Status, DateTime? NextDueUtc);

/// <summary>
/// Restore-test recency (U12), kept distinct from ordinary backup health: a backup can be On Track
/// while its restore-test coverage is Overdue or has never existed at all. Pure (no I/O).
/// </summary>
public static class RestoreTestRecencyCalculator
{
    public static RestoreTestRecencyResult Compute(DateTime? lastSuccessfulRestoreUtc, int intervalDays, DateTime nowUtc)
    {
        if (lastSuccessfulRestoreUtc is null)
        {
            return new RestoreTestRecencyResult(RestoreTestRecency.NeverTested, null);
        }

        var nextDue = lastSuccessfulRestoreUtc.Value.AddDays(intervalDays);
        var status = nextDue <= nowUtc ? RestoreTestRecency.Overdue : RestoreTestRecency.Current;
        return new RestoreTestRecencyResult(status, nextDue);
    }
}
