using DBTracker.Core.Entities;
using DBTracker.Core.Enums;
using DBTracker.Core.Services;
using DBTracker.Infrastructure.Audit;
using Microsoft.EntityFrameworkCore;

namespace DBTracker.Infrastructure.Ownership;

/// <summary>Named, versioned backup policies. Applying one copies its values onto instances after a preview; editing a policy never auto-applies it.</summary>
public class BackupPolicyService(IDbContextFactory<DBTrackerDbContext> factory, AuditRecorder audit)
{
    public async Task<List<BackupPolicy>> ListAsync()
    {
        using var db = await factory.CreateDbContextAsync();
        return await db.BackupPolicies.AsNoTracking().OrderBy(p => p.Name).ToListAsync();
    }

    public async Task<BackupPolicy?> GetAsync(int id)
    {
        using var db = await factory.CreateDbContextAsync();
        return await db.BackupPolicies.AsNoTracking().FirstOrDefaultAsync(p => p.Id == id);
    }

    /// <summary>Audit actions Trends shows as policy changes.</summary>
    public static readonly string[] PolicyChangeActions = ["BackupPolicyCreated", "BackupPolicyUpdated"];

    /// <summary>Returns false (no-op) if a policy with this name already exists.</summary>
    public async Task<bool> CreateAsync(string name, int expectedBackupIntervalHours, int? retentionPolicyDays, int? restoreTestIntervalDays, string createdBy, string createdByUserId = "")
    {
        using var db = await factory.CreateDbContextAsync();
        if (await db.BackupPolicies.AnyAsync(p => p.Name == name))
        {
            return false;
        }

        var policy = new BackupPolicy
        {
            Name = name,
            Version = 1,
            ExpectedBackupIntervalHours = expectedBackupIntervalHours,
            RetentionPolicyDays = retentionPolicyDays,
            RestoreTestIntervalDays = restoreTestIntervalDays,
            UpdatedBy = createdBy
        };
        db.BackupPolicies.Add(policy);
        await db.SaveChangesAsync();

        await audit.RecordUserActionAsync(createdByUserId, createdBy, "BackupPolicyCreated", "BackupPolicy", policy.Id.ToString(), policy.Name,
            afterJson: ValuesJson(policy));
        return true;
    }

    /// <summary>What saving an edit would change: the policy's own values and, for every active instance using the policy, the effect of
    /// re-applying the new version (including health under the new cadence). Nothing is written.</summary>
    public async Task<PolicyEditImpact?> PreviewEditAsync(int id, int expectedBackupIntervalHours, int? retentionPolicyDays, int? restoreTestIntervalDays)
    {
        using var db = await factory.CreateDbContextAsync();
        var policy = await db.BackupPolicies.AsNoTracking().FirstOrDefaultAsync(p => p.Id == id);
        if (policy is null)
        {
            return null;
        }

        var now = DateTime.UtcNow;
        var linked = await db.DatabaseInstances.AsNoTracking()
            .Where(i => i.AppliedPolicyId == id && !i.IsArchived)
            .Select(i => new
            {
                Instance = i,
                Latest = i.BackupRecords.Where(r => r.TimestampUtc <= now).OrderByDescending(r => r.TimestampUtc).ThenByDescending(r => r.Id).FirstOrDefault(),
                LastSuccess = i.BackupRecords.Where(r => r.Status == BackupStatus.Success && r.TimestampUtc <= now)
                    .OrderByDescending(r => r.TimestampUtc).Select(r => (DateTime?)r.TimestampUtc).FirstOrDefault()
            })
            .ToListAsync();

        var targets = linked.Select(l => new PolicyEditImpactTarget(
                new PolicyTargetInstance(l.Instance.Id, l.Instance.Name, l.Instance.ExpectedBackupIntervalHours, l.Instance.RetentionPolicyDays,
                    l.Instance.RestoreTestIntervalDaysOverride, l.Instance.AppliedPolicyId, l.Instance.AppliedPolicyVersion),
                l.LastSuccess,
                BackupHealthCalculator.ComputeFromLatest(l.Instance, l.Latest, now)))
            .ToList();

        return PolicyEditImpactBuilder.Build(policy, expectedBackupIntervalHours, retentionPolicyDays, restoreTestIntervalDays, targets, now);
    }

    /// <summary>Ids of active instances that use this policy, for "apply the new version" after an edit.</summary>
    public async Task<List<int>> LinkedInstanceIdsAsync(int id)
    {
        using var db = await factory.CreateDbContextAsync();
        return await db.DatabaseInstances.AsNoTracking().Where(i => i.AppliedPolicyId == id && !i.IsArchived).Select(i => i.Id).ToListAsync();
    }

    /// <summary>Edits a policy and bumps its Version; the before/after values go to the audit trail. Returns the validation errors,
    /// or "the policy changed" when <paramref name="expectedVersion"/> no longer matches (someone else saved first).</summary>
    public async Task<IReadOnlyList<string>> UpdateAsync(int id, int expectedBackupIntervalHours, int? retentionPolicyDays, int? restoreTestIntervalDays,
        string updatedByUserId, string updatedByName, int? expectedVersion = null, string reason = "")
    {
        var errors = PolicyEditImpactBuilder.Validate(expectedBackupIntervalHours, retentionPolicyDays, restoreTestIntervalDays);
        if (errors.Count > 0)
        {
            return errors;
        }

        using var db = await factory.CreateDbContextAsync();
        var policy = await db.BackupPolicies.FindAsync(id);
        if (policy is null)
        {
            return ["This policy no longer exists."];
        }
        if (expectedVersion is { } v && policy.Version != v)
        {
            return [$"Someone saved version {policy.Version} of this policy after you opened it. Reopen the editor to review their change."];
        }

        var before = $"{{\"ExpectedBackupIntervalHours\":{policy.ExpectedBackupIntervalHours},\"RetentionPolicyDays\":{policy.RetentionPolicyDays},\"RestoreTestIntervalDays\":{policy.RestoreTestIntervalDays},\"Version\":{policy.Version}}}";

        policy.ExpectedBackupIntervalHours = expectedBackupIntervalHours;
        policy.RetentionPolicyDays = retentionPolicyDays;
        policy.RestoreTestIntervalDays = restoreTestIntervalDays;
        policy.Version += 1;
        policy.UpdatedAtUtc = DateTime.UtcNow;
        policy.UpdatedBy = updatedByName;
        await db.SaveChangesAsync();

        var after = $"{{\"ExpectedBackupIntervalHours\":{policy.ExpectedBackupIntervalHours},\"RetentionPolicyDays\":{policy.RetentionPolicyDays},\"RestoreTestIntervalDays\":{policy.RestoreTestIntervalDays},\"Version\":{policy.Version}}}";
        await audit.RecordUserActionAsync(updatedByUserId, updatedByName, "BackupPolicyUpdated", "BackupPolicy", id.ToString(), policy.Name, reason: reason.Trim(), beforeJson: before, afterJson: after);
        return [];
    }

    private static string ValuesJson(BackupPolicy p) => System.Text.Json.JsonSerializer.Serialize(new
    {
        p.ExpectedBackupIntervalHours,
        p.RetentionPolicyDays,
        p.RestoreTestIntervalDays,
        p.Version
    });

    public async Task<List<PolicyTargetInstance>> GetTargetsAsync(IReadOnlyCollection<int> instanceIds)
    {
        using var db = await factory.CreateDbContextAsync();
        return await db.DatabaseInstances.AsNoTracking()
            .Where(i => instanceIds.Contains(i.Id))
            .Select(i => new PolicyTargetInstance(i.Id, i.Name, i.ExpectedBackupIntervalHours, i.RetentionPolicyDays, i.RestoreTestIntervalDaysOverride, i.AppliedPolicyId, i.AppliedPolicyVersion))
            .ToListAsync();
    }

    /// <summary>Applies the (already-previewed) policy to every target instance and records one audit event per instance.</summary>
    public async Task ApplyAsync(int policyId, IReadOnlyCollection<int> instanceIds, string appliedByUserId, string appliedByName)
    {
        using var db = await factory.CreateDbContextAsync();
        var policy = await db.BackupPolicies.AsNoTracking().FirstOrDefaultAsync(p => p.Id == policyId);
        if (policy is null)
        {
            return;
        }

        var instances = await db.DatabaseInstances.Where(i => instanceIds.Contains(i.Id)).ToListAsync();
        foreach (var instance in instances)
        {
            var before = $"{{\"ExpectedBackupIntervalHours\":{instance.ExpectedBackupIntervalHours},\"RetentionPolicyDays\":{instance.RetentionPolicyDays},\"RestoreTestIntervalDaysOverride\":{instance.RestoreTestIntervalDaysOverride}}}";
            BackupPolicyApplyPreviewBuilder.Apply(instance, policy);
            instance.UpdatedAtUtc = DateTime.UtcNow;
            var after = $"{{\"ExpectedBackupIntervalHours\":{instance.ExpectedBackupIntervalHours},\"RetentionPolicyDays\":{instance.RetentionPolicyDays},\"RestoreTestIntervalDaysOverride\":{instance.RestoreTestIntervalDaysOverride}}}";
            await audit.RecordUserActionAsync(appliedByUserId, appliedByName, "BackupPolicyApplied", "DatabaseInstance", instance.Id.ToString(), instance.Name,
                reason: $"Policy '{policy.Name}' v{policy.Version}", beforeJson: before, afterJson: after);
        }

        await db.SaveChangesAsync();
    }
}
