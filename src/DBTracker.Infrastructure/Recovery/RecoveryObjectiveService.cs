using DBTracker.Core.Entities;
using DBTracker.Core.Enums;
using DBTracker.Core.Services;
using DBTracker.Infrastructure.Audit;
using Microsoft.EntityFrameworkCore;

namespace DBTracker.Infrastructure.Recovery;

public record RecoveryEvidenceView(
    DatabaseInstance Instance,
    RecoveryObjective? Objective,
    IReadOnlyList<BackupChainRequirement> ChainRequirements,
    BackupChainCoverageResult Coverage,
    IReadOnlyList<RestoreEvent> RecentRestores);

/// <summary>
/// D16 — business recovery objectives and backup-chain-coverage evidence. Coverage itself is always
/// computed live from BackupChainCoverageCalculator (pure) over whatever BackupChainRequirement rows
/// and BackupRecords actually exist — nothing here caches or pre-asserts a status, and an instance
/// with no requirements recorded is reported Unknown rather than defaulted to any other state.
/// </summary>
public class RecoveryObjectiveService(IDbContextFactory<DBTrackerDbContext> factory, AuditRecorder audit)
{
    public async Task<RecoveryEvidenceView?> GetForInstanceAsync(int instanceId)
    {
        using var db = await factory.CreateDbContextAsync();
        var instance = await db.DatabaseInstances.AsNoTracking().FirstOrDefaultAsync(i => i.Id == instanceId);
        if (instance is null)
        {
            return null;
        }

        var objective = await db.RecoveryObjectives.AsNoTracking().FirstOrDefaultAsync(o => o.DatabaseInstanceId == instanceId);
        var requirements = await db.BackupChainRequirements.AsNoTracking()
            .Where(r => r.DatabaseInstanceId == instanceId)
            .OrderBy(r => r.MemberType)
            .ToListAsync();
        var records = await db.BackupRecords.AsNoTracking()
            .Where(r => r.DatabaseInstanceId == instanceId)
            .ToListAsync();
        var restores = await db.RestoreEvents.AsNoTracking()
            .Where(r => r.DatabaseInstanceId == instanceId)
            .OrderByDescending(r => r.TimestampUtc)
            .Take(5)
            .ToListAsync();

        var coverage = BackupChainCoverageCalculator.Compute(requirements, records, DateTime.UtcNow, instance.ExpectedBackupIntervalHours);

        return new RecoveryEvidenceView(instance, objective, requirements, coverage, restores);
    }

    public async Task SetObjectiveAsync(int instanceId, int? rpoMinutes, int? rtoMinutes, string encryptionKeyNote, string offsiteCopyNote, string notes, string actorName)
    {
        using var db = await factory.CreateDbContextAsync();
        var objective = await db.RecoveryObjectives.FirstOrDefaultAsync(o => o.DatabaseInstanceId == instanceId);
        if (objective is null)
        {
            objective = new RecoveryObjective { DatabaseInstanceId = instanceId };
            db.RecoveryObjectives.Add(objective);
        }

        objective.RpoTargetMinutes = rpoMinutes;
        objective.RtoTargetMinutes = rtoMinutes;
        objective.EncryptionKeyReferenceNote = encryptionKeyNote;
        objective.OffsiteCopyReferenceNote = offsiteCopyNote;
        objective.Notes = notes;
        objective.UpdatedAtUtc = DateTime.UtcNow;
        objective.UpdatedBy = actorName;
        await db.SaveChangesAsync();

        var instance = await db.DatabaseInstances.FirstAsync(i => i.Id == instanceId);
        await audit.RecordUserActionAsync("", actorName, "RecoveryObjectiveSet", "DatabaseInstance", instanceId.ToString(), instance.Name,
            reason: $"RPO {rpoMinutes?.ToString() ?? "unset"}m / RTO {rtoMinutes?.ToString() ?? "unset"}m");
    }

    public async Task<BackupChainRequirement> AddChainRequirementAsync(int instanceId, ChainMemberType memberType, bool isRequired, int? maxAgeHoursOverride, string notes, string actorName)
    {
        using var db = await factory.CreateDbContextAsync();
        var existing = await db.BackupChainRequirements.FirstOrDefaultAsync(r => r.DatabaseInstanceId == instanceId && r.MemberType == memberType);
        if (existing is not null)
        {
            existing.IsRequired = isRequired;
            existing.MaxAgeHoursOverride = maxAgeHoursOverride;
            existing.Notes = notes;
            await db.SaveChangesAsync();

            var instanceForUpdate = await db.DatabaseInstances.FirstAsync(i => i.Id == instanceId);
            await audit.RecordUserActionAsync("", actorName, "BackupChainRequirementUpdated", "DatabaseInstance", instanceId.ToString(), instanceForUpdate.Name,
                reason: $"{memberType}: required={isRequired}");
            return existing;
        }

        var requirement = new BackupChainRequirement
        {
            DatabaseInstanceId = instanceId,
            MemberType = memberType,
            IsRequired = isRequired,
            MaxAgeHoursOverride = maxAgeHoursOverride,
            Notes = notes,
            CreatedBy = actorName
        };
        db.BackupChainRequirements.Add(requirement);
        await db.SaveChangesAsync();

        var instance = await db.DatabaseInstances.FirstAsync(i => i.Id == instanceId);
        await audit.RecordUserActionAsync("", actorName, "BackupChainRequirementRecorded", "DatabaseInstance", instanceId.ToString(), instance.Name,
            reason: $"{memberType}: required={isRequired}");
        return requirement;
    }

    public async Task RemoveChainRequirementAsync(int requirementId, string actorName)
    {
        using var db = await factory.CreateDbContextAsync();
        var requirement = await db.BackupChainRequirements.FirstOrDefaultAsync(r => r.Id == requirementId);
        if (requirement is null)
        {
            return;
        }

        db.BackupChainRequirements.Remove(requirement);
        await db.SaveChangesAsync();

        await audit.RecordUserActionAsync("", actorName, "BackupChainRequirementRemoved", "DatabaseInstance", requirement.DatabaseInstanceId.ToString(), requirement.MemberType.ToString());
    }
}
