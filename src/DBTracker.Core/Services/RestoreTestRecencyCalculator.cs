using DBTracker.Core.Enums;

namespace DBTracker.Core.Services;

public record RestoreTestRecencyResult(RestoreTestRecency Status, DateTime? NextDueUtc);

/// <summary>Restore-test recency, separate from backup health: backups can be On Track while restore tests are Overdue or missing.</summary>
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
