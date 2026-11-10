using DBTracker.Core.Entities;
using DBTracker.Core.Enums;
using DBTracker.Core.Services;
using DBTracker.Infrastructure.Audit;
using Microsoft.EntityFrameworkCore;

namespace DBTracker.Infrastructure.Recovery;

public record RetentionEvidenceView(DatabaseInstance Instance, IReadOnlyList<BackupCopyLocation> Copies, RetentionCoverageResult Coverage);

/// <summary>
/// D17 — backup-copy availability and retention evidence. StoragePathOrReference is masked by role
/// at read time (MaskSensitivePaths), never only in the UI layer, so a CSV export built from this
/// same read path can never leak the raw path to a role that shouldn't see it.
/// </summary>
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

        var masked = rawCopies.Select(c => new BackupCopyLocation
        {
            Id = c.Id,
            DatabaseInstanceId = c.DatabaseInstanceId,
            Label = c.Label,
            IsOffsite = c.IsOffsite,
            IsImmutableFlagged = c.IsImmutableFlagged,
            EncryptionExpected = c.EncryptionExpected,
            StoragePathOrReference = BackupCopyLocationMasker.Mask(c.StoragePathOrReference, canViewSensitivePaths),
            FirstDiscoveredAtUtc = c.FirstDiscoveredAtUtc,
            VerificationStatus = c.VerificationStatus,
            LastVerifiedAtUtc = c.LastVerifiedAtUtc,
            LastVerifiedBy = c.LastVerifiedBy,
            VerificationNotes = c.VerificationNotes,
            LegalHoldReference = c.LegalHoldReference,
            Notes = c.Notes
        }).ToList();

        return new RetentionEvidenceView(instance, masked, coverage);
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
        await audit.RecordUserActionAsync("", actorName, "BackupCopyLocationRecorded", "DatabaseInstance", copy.DatabaseInstanceId.ToString(), instance.Name,
            reason: $"{copy.Label} ({(copy.IsOffsite ? "offsite" : "primary")})");
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
}
