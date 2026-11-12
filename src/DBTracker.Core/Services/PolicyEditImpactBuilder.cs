using DBTracker.Core.Entities;
using DBTracker.Core.Enums;

namespace DBTracker.Core.Services;

/// <summary>An instance linked to the policy being edited, with what its health rests on today.</summary>
public record PolicyEditImpactTarget(PolicyTargetInstance Instance, DateTime? LastSuccessUtc, BackupHealth CurrentHealth);

/// <summary>What re-applying the edited policy would do to one linked instance.</summary>
public record PolicyEditImpactRow(
    int InstanceId, string InstanceName, int? AppliedVersion, IReadOnlyList<BulkEditFieldDiff> ReapplyChanges,
    BackupHealth CurrentHealth, BackupHealth HealthAfterReapply)
{
    public bool HealthChanges => CurrentHealth != HealthAfterReapply;
}

public record PolicyEditImpact(IReadOnlyList<BulkEditFieldDiff> PolicyChanges, IReadOnlyList<PolicyEditImpactRow> Rows, IReadOnlyList<string> Errors)
{
    public int LinkedCount => Rows.Count;
    public int WouldChangeCount => Rows.Count(r => r.ReapplyChanges.Count > 0);
    public int BecomesOverdueCount => Rows.Count(r => r.CurrentHealth == BackupHealth.OnTrack && r.HealthAfterReapply == BackupHealth.Overdue);
    public int BecomesOnTrackCount => Rows.Count(r => r.CurrentHealth == BackupHealth.Overdue && r.HealthAfterReapply == BackupHealth.OnTrack);
}

/// <summary>Preview of a policy edit before it is saved: the policy's own before/after, and for each instance that uses the policy,
/// what re-applying the new version would change, including health under the new cadence. Saving never re-applies on its own.</summary>
public static class PolicyEditImpactBuilder
{
    public static PolicyEditImpact Build(
        BackupPolicy current, int newIntervalHours, int? newRetentionDays, int? newRestoreTestDays,
        IReadOnlyList<PolicyEditImpactTarget> linked, DateTime nowUtc)
    {
        var errors = Validate(newIntervalHours, newRetentionDays, newRestoreTestDays);

        var policyChanges = new List<BulkEditFieldDiff>();
        Add(policyChanges, "ExpectedBackupIntervalHours", current.ExpectedBackupIntervalHours.ToString(), newIntervalHours.ToString());
        Add(policyChanges, "RetentionPolicyDays", current.RetentionPolicyDays?.ToString() ?? "", newRetentionDays?.ToString() ?? "");
        Add(policyChanges, "RestoreTestIntervalDays", current.RestoreTestIntervalDays?.ToString() ?? "", newRestoreTestDays?.ToString() ?? "");

        var proposed = new BackupPolicy
        {
            Id = current.Id,
            Name = current.Name,
            Version = current.Version + 1,
            ExpectedBackupIntervalHours = newIntervalHours,
            RetentionPolicyDays = newRetentionDays,
            RestoreTestIntervalDays = newRestoreTestDays
        };

        var previews = BackupPolicyApplyPreviewBuilder.Preview(linked.Select(l => l.Instance).ToList(), proposed);
        var rows = linked.Zip(previews, (target, preview) => new PolicyEditImpactRow(
                target.Instance.Id, target.Instance.Name, target.Instance.AppliedPolicyVersion, preview.Changes,
                target.CurrentHealth, ProjectHealth(target, newIntervalHours, nowUtc)))
            .OrderByDescending(r => r.HealthChanges)
            .ThenBy(r => r.InstanceName, StringComparer.OrdinalIgnoreCase)
            .ToList();

        return new PolicyEditImpact(policyChanges, rows, errors);
    }

    public static IReadOnlyList<string> Validate(int intervalHours, int? retentionDays, int? restoreTestDays)
    {
        var errors = new List<string>();
        if (intervalHours <= 0) errors.Add("Expected backup interval must be more than 0 hours.");
        if (retentionDays is < 0) errors.Add("Retention can't be negative.");
        if (restoreTestDays is <= 0) errors.Add("Restore-test interval must be more than 0 days, or empty.");
        return errors;
    }

    /// <summary>Only OnTrack and Overdue depend on the cadence; Failed and Never backed up stay as they are.</summary>
    private static BackupHealth ProjectHealth(PolicyEditImpactTarget target, int newIntervalHours, DateTime nowUtc)
    {
        if (target.CurrentHealth is not (BackupHealth.OnTrack or BackupHealth.Overdue) || target.LastSuccessUtc is not { } last)
        {
            return target.CurrentHealth;
        }

        return nowUtc > last.AddHours(newIntervalHours) ? BackupHealth.Overdue : BackupHealth.OnTrack;
    }

    private static void Add(List<BulkEditFieldDiff> diffs, string field, string before, string after)
    {
        if (!string.Equals(before, after, StringComparison.Ordinal))
        {
            diffs.Add(new BulkEditFieldDiff(field, before, after));
        }
    }
}
