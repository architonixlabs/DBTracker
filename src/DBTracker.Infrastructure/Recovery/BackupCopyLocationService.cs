using DBTracker.Core.Entities;
using DBTracker.Core.Enums;
using DBTracker.Core.Services;
using DBTracker.Infrastructure.Audit;
using Microsoft.EntityFrameworkCore;

namespace DBTracker.Infrastructure.Recovery;

public record RetentionEvidenceView(DatabaseInstance Instance, IReadOnlyList<BackupCopyLocation> Copies, RetentionCoverageResult Coverage,
    IReadOnlyList<CopyExpectationResult> Expectations);

/// <summary>Backup-copy availability and retention evidence. Paths are masked by role at read time, so exports built on this read can't leak them.</summary>
public class BackupCopyLocationService(IDbContextFactory<DBTrackerDbContext> factory, AuditRecorder audit)
{
    public const int DefaultStaleAfterDays = 30;

    public async Task<List<BackupCopyLocation>> ListForInstanceAsync(int instanceId, bool canViewSensitivePaths)
    {
        using var db = await factory.CreateDbContextAsync();
        var copies = await db.BackupCopyLocations.AsNoTracking()
            .Where(c => c.DatabaseInstanceId == instanceId)
            .OrderBy(c => c.Label)
            .ToListAsync();

        foreach (var copy in copies)
        {
            copy.StoragePathOrReference = BackupCopyLocationMasker.Mask(copy.StoragePathOrReference, canViewSensitivePaths);
        }
        return copies;
    }

    public async Task<RetentionEvidenceView?> GetRetentionEvidenceAsync(int instanceId, bool canViewSensitivePaths)
    {
        using var db = await factory.CreateDbContextAsync();
        var instance = await db.DatabaseInstances.AsNoTracking().FirstOrDefaultAsync(i => i.Id == instanceId);
        if (instance is null)
        {
            return null;
        }

        var rawCopies = await db.BackupCopyLocations.AsNoTracking()
            .Where(c => c.DatabaseInstanceId == instanceId)
            .OrderBy(c => c.Label)
            .ToListAsync();

        var coverage = RetentionCoverageComparer.Compare(instance.RetentionPolicyDays, rawCopies, DateTime.UtcNow, DefaultStaleAfterDays);
        var objective = await db.RecoveryObjectives.AsNoTracking().FirstOrDefaultAsync(o => o.DatabaseInstanceId == instanceId);
        var expectations = BackupCopyExpectationEvaluator.Evaluate(objective, rawCopies, DateTime.UtcNow, DefaultStaleAfterDays);

        var masked = rawCopies.Select(c => new BackupCopyLocation
        {
            Id = c.Id,
            DatabaseInstanceId = c.DatabaseInstanceId,
            Label = c.Label,
            IsOffsite = c.IsOffsite,
            IsImmutableFlagged = c.IsImmutableFlagged,
            EncryptionExpected = c.EncryptionExpected,
            EncryptionConfirmed = c.EncryptionConfirmed,
            StoragePathOrReference = BackupCopyLocationMasker.Mask(c.StoragePathOrReference, canViewSensitivePaths),
            FirstDiscoveredAtUtc = c.FirstDiscoveredAtUtc,
            VerificationStatus = c.VerificationStatus,
            LastVerifiedAtUtc = c.LastVerifiedAtUtc,
            LastVerifiedBy = c.LastVerifiedBy,
            VerificationNotes = c.VerificationNotes,
            LegalHoldReference = c.LegalHoldReference,
            Notes = c.Notes
        }).ToList();

        return new RetentionEvidenceView(instance, masked, coverage, expectations);
    }

    public async Task<BackupCopyLocation> AddAsync(BackupCopyLocation copy, string actorName)
    {
        using var db = await factory.CreateDbContextAsync();
        copy.FirstDiscoveredAtUtc ??= DateTime.UtcNow;
        copy.CreatedAtUtc = DateTime.UtcNow;
        copy.UpdatedAtUtc = DateTime.UtcNow;
        copy.UpdatedBy = actorName;
        db.BackupCopyLocations.Add(copy);
        await db.SaveChangesAsync();

        var instance = await db.DatabaseInstances.FirstAsync(i => i.Id == copy.DatabaseInstanceId);
        var flags = new List<string> { copy.IsOffsite ? "offsite" : "primary" };
        if (copy.IsImmutableFlagged)
        {
            flags.Add("immutable-flagged");
        }
        if (copy.EncryptionExpected)
        {
            flags.Add("encryption expected");
        }
        if (!string.IsNullOrWhiteSpace(copy.LegalHoldReference))
        {
            flags.Add($"legal hold {copy.LegalHoldReference}");
        }
        await audit.RecordUserActionAsync("", actorName, "BackupCopyLocationRecorded", "DatabaseInstance", copy.DatabaseInstanceId.ToString(), instance.Name,
            reason: $"{copy.Label} ({string.Join(", ", flags)})");
        return copy;
    }

    public async Task SetVerificationAsync(int copyId, CopyVerificationStatus status, string notes, string actorName)
    {
        using var db = await factory.CreateDbContextAsync();
        var copy = await db.BackupCopyLocations.FirstOrDefaultAsync(c => c.Id == copyId);
        if (copy is null)
        {
            return;
        }

        copy.VerificationStatus = status;
        copy.LastVerifiedAtUtc = DateTime.UtcNow;
        copy.LastVerifiedBy = actorName;
        copy.VerificationNotes = notes;
        copy.UpdatedAtUtc = DateTime.UtcNow;
        copy.UpdatedBy = actorName;
        await db.SaveChangesAsync();

        var instance = await db.DatabaseInstances.FirstAsync(i => i.Id == copy.DatabaseInstanceId);
        await audit.RecordUserActionAsync("", actorName, "BackupCopyVerificationRecorded", "DatabaseInstance", copy.DatabaseInstanceId.ToString(), instance.Name,
            reason: $"{copy.Label}: {status}");
    }

    /// <summary>Records what a check found about encryption at rest for a copy, without touching its availability status.</summary>
    public async Task SetEncryptionFindingAsync(int copyId, bool encrypted, string actorName)
    {
        using var db = await factory.CreateDbContextAsync();
        var copy = await db.BackupCopyLocations.FirstOrDefaultAsync(c => c.Id == copyId);
        if (copy is null)
        {
            return;
        }

        copy.EncryptionConfirmed = encrypted;
        copy.UpdatedAtUtc = DateTime.UtcNow;
        copy.UpdatedBy = actorName;
        await db.SaveChangesAsync();

        var instance = await db.DatabaseInstances.FirstAsync(i => i.Id == copy.DatabaseInstanceId);
        await audit.RecordUserActionAsync("", actorName, "BackupCopyEncryptionRecorded", "DatabaseInstance", copy.DatabaseInstanceId.ToString(), instance.Name,
            reason: $"{copy.Label}: {(encrypted ? "encryption confirmed" : "found not encrypted")}");
    }

    /// <summary>Sets or clears the legal-hold reference on a copy. A reference is a pointer to a hold, not a legal decision made here.</summary>
    public async Task<(bool Ok, string Reason)> SetLegalHoldAsync(int copyId, string reference, string actorName, IReadOnlyCollection<string> actorRoles)
    {
        if (!actorRoles.Contains(Identity.Roles.Admin) && !actorRoles.Contains(Identity.Roles.Dba))
        {
            return (false, "Only Admin and DBA users can change legal-hold references.");
        }
        var trimmed = reference.Trim();
        if (trimmed.Length > 300)
        {
            return (false, "Keep the legal-hold reference under 300 characters.");
        }

        using var db = await factory.CreateDbContextAsync();
        var copy = await db.BackupCopyLocations.FirstOrDefaultAsync(c => c.Id == copyId);
        if (copy is null)
        {
            return (false, "Copy location not found.");
        }

        var previous = copy.LegalHoldReference;
        copy.LegalHoldReference = trimmed;
        copy.UpdatedAtUtc = DateTime.UtcNow;
        copy.UpdatedBy = actorName;
        await db.SaveChangesAsync();

        var instance = await db.DatabaseInstances.FirstAsync(i => i.Id == copy.DatabaseInstanceId);
        await audit.RecordUserActionAsync("", actorName, string.IsNullOrEmpty(trimmed) ? "BackupCopyLegalHoldCleared" : "BackupCopyLegalHoldSet",
            "DatabaseInstance", copy.DatabaseInstanceId.ToString(), instance.Name,
            reason: $"{copy.Label}: \"{previous}\" -> \"{trimmed}\"");
        return (true, "");
    }
}
