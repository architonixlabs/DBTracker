using DBTracker.Core.Entities;
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

    /// <summary>Returns false (no-op) if a policy with this name already exists.</summary>
    public async Task<bool> CreateAsync(string name, int expectedBackupIntervalHours, int? retentionPolicyDays, int? restoreTestIntervalDays, string createdBy)
    {
        using var db = await factory.CreateDbContextAsync();
        if (await db.BackupPolicies.AnyAsync(p => p.Name == name))
        {
            return false;
        }

        db.BackupPolicies.Add(new BackupPolicy
        {
            Name = name,
            Version = 1,
            ExpectedBackupIntervalHours = expectedBackupIntervalHours,
            RetentionPolicyDays = retentionPolicyDays,
            RestoreTestIntervalDays = restoreTestIntervalDays,
            UpdatedBy = createdBy
        });
        await db.SaveChangesAsync();
        return true;
    }

    /// <summary>Edits a policy and bumps its Version; the before/after values go to the audit trail.</summary>
    public async Task UpdateAsync(int id, int expectedBackupIntervalHours, int? retentionPolicyDays, int? restoreTestIntervalDays, string updatedByUserId, string updatedByName)
    {
        using var db = await factory.CreateDbContextAsync();
        var policy = await db.BackupPolicies.FindAsync(id);
        if (policy is null)
        {
            return;
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
        await audit.RecordUserActionAsync(updatedByUserId, updatedByName, "BackupPolicyUpdated", "BackupPolicy", id.ToString(), policy.Name, beforeJson: before, afterJson: after);
    }

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
