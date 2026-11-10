using DBTracker.Core.Entities;

namespace DBTracker.Core.Services;

/// <summary>Minimal projection of the fields a BackupPolicy can set on an instance — enough to preview/apply without loading full DatabaseInstance tracking state.</summary>
public record PolicyTargetInstance(int Id, string Name, int ExpectedBackupIntervalHours, int? RetentionPolicyDays, int? RestoreTestIntervalDaysOverride, int? AppliedPolicyId, int? AppliedPolicyVersion);

public record PolicyApplyRowPreview(int InstanceId, string InstanceName, IReadOnlyList<BulkEditFieldDiff> Changes, bool AlreadyUpToDate);

/// <summary>
/// Builds the "what would change" preview before applying a reusable BackupPolicy (U21) to a set of
/// instances — building on the same before/after diff pattern U17's BulkEditPreviewBuilder already
/// established for bulk owner/tag edits. Pure (no DB writes); the caller applies the same policy
/// after the user confirms, via <see cref="Apply"/>.
/// </summary>
public static class BackupPolicyApplyPreviewBuilder
{
    public static IReadOnlyList<PolicyApplyRowPreview> Preview(IReadOnlyList<PolicyTargetInstance> instances, BackupPolicy policy)
    {
        var rows = new List<PolicyApplyRowPreview>();

        foreach (var instance in instances)
        {
            var diffs = new List<BulkEditFieldDiff>();
            AddIfChanged(diffs, "ExpectedBackupIntervalHours", instance.ExpectedBackupIntervalHours.ToString(), policy.ExpectedBackupIntervalHours.ToString());
            AddIfChanged(diffs, "RetentionPolicyDays", instance.RetentionPolicyDays?.ToString() ?? "", policy.RetentionPolicyDays?.ToString() ?? "");
            AddIfChanged(diffs, "RestoreTestIntervalDaysOverride", instance.RestoreTestIntervalDaysOverride?.ToString() ?? "", policy.RestoreTestIntervalDays?.ToString() ?? "");

            // Already pointed at this exact policy version with no field-level diff — applying again would be a no-op.
            var alreadyUpToDate = diffs.Count == 0 && instance.AppliedPolicyId == policy.Id && instance.AppliedPolicyVersion == policy.Version;

            rows.Add(new PolicyApplyRowPreview(instance.Id, instance.Name, diffs, alreadyUpToDate));
        }

        return rows;
    }

    /// <summary>Applies the policy's values onto an in-memory instance — the caller persists it. Deliberately mirrors Preview's field list exactly, so what's shown is what's applied.</summary>
    public static void Apply(DatabaseInstance instance, BackupPolicy policy)
    {
        instance.ExpectedBackupIntervalHours = policy.ExpectedBackupIntervalHours;
        instance.RetentionPolicyDays = policy.RetentionPolicyDays;
        instance.RestoreTestIntervalDaysOverride = policy.RestoreTestIntervalDays;
        instance.AppliedPolicyId = policy.Id;
        instance.AppliedPolicyVersion = policy.Version;
    }

    private static void AddIfChanged(List<BulkEditFieldDiff> diffs, string field, string before, string after)
    {
        if (!string.Equals(before, after, StringComparison.Ordinal))
        {
            diffs.Add(new BulkEditFieldDiff(field, before, after));
        }
    }
}
