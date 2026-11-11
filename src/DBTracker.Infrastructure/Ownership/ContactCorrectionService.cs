using DBTracker.Core.Entities;
using DBTracker.Core.Enums;
using DBTracker.Core.Services;
using DBTracker.Infrastructure.Audit;
using Microsoft.EntityFrameworkCore;

namespace DBTracker.Infrastructure.Ownership;

/// <summary>Contact correction requests. Anyone signed in can submit; CanEditPolicy roles approve, which applies only the filled-in fields and writes an audit event and change-log entry.</summary>
public class ContactCorrectionService(IDbContextFactory<DBTrackerDbContext> factory, AuditRecorder audit)
{
    public async Task<List<ContactCorrectionRequest>> ListPendingAsync()
    {
        using var db = await factory.CreateDbContextAsync();
        return await db.ContactCorrectionRequests.AsNoTracking()
            .Include(r => r.DatabaseInstance)
            .Where(r => r.Status == ContactCorrectionStatus.Pending)
            .OrderBy(r => r.RequestedAtUtc)
            .ToListAsync();
    }

    public async Task<List<ContactCorrectionRequest>> ListForInstanceAsync(int instanceId)
    {
        using var db = await factory.CreateDbContextAsync();
        return await db.ContactCorrectionRequests.AsNoTracking()
            .Where(r => r.DatabaseInstanceId == instanceId)
            .OrderByDescending(r => r.RequestedAtUtc)
            .ToListAsync();
    }

    /// <summary>Returns false if the request proposes no actual field change — rejected before it's even created.</summary>
    public async Task<bool> SubmitAsync(ContactCorrectionRequest request)
    {
        if (!ContactCorrectionApplier.HasAnyProposedChange(request) || string.IsNullOrWhiteSpace(request.Note))
        {
            return false;
        }

        using var db = await factory.CreateDbContextAsync();
        db.ContactCorrectionRequests.Add(request);
        await db.SaveChangesAsync();
        await audit.RecordUserActionAsync(request.RequestedByUserId, request.RequestedByName, "ContactCorrectionRequested", "DatabaseInstance", request.DatabaseInstanceId.ToString(), request.RequestedByName, reason: request.Note);
        return true;
    }

    public async Task<bool> ApproveAsync(int requestId, string reviewerUserId, string reviewerName, string reviewNote)
    {
        using var db = await factory.CreateDbContextAsync();
        var request = await db.ContactCorrectionRequests.FirstOrDefaultAsync(r => r.Id == requestId && r.Status == ContactCorrectionStatus.Pending);
        var instance = request is null ? null : await db.DatabaseInstances.FirstOrDefaultAsync(i => i.Id == request.DatabaseInstanceId);
        if (request is null || instance is null)
        {
            return false;
        }

        var before = Clone(instance);
        ContactCorrectionApplier.Apply(instance, request);
        instance.UpdatedAtUtc = DateTime.UtcNow;

        request.Status = ContactCorrectionStatus.Approved;
        request.ReviewedByUserId = reviewerUserId;
        request.ReviewedByName = reviewerName;
        request.ReviewedAtUtc = DateTime.UtcNow;
        request.ReviewNote = reviewNote;

        var changes = InstanceChangeLogBuilder.Diff(before, instance, reviewerName, DateTime.UtcNow);
        if (changes.Count > 0)
        {
            db.InstanceChangeLogs.AddRange(changes);
        }

        await db.SaveChangesAsync();
        await audit.RecordUserActionAsync(reviewerUserId, reviewerName, "ContactCorrectionApproved", "DatabaseInstance", instance.Id.ToString(), instance.Name, reason: reviewNote);
        return true;
    }

    public async Task<bool> RejectAsync(int requestId, string reviewerUserId, string reviewerName, string reviewNote)
    {
        using var db = await factory.CreateDbContextAsync();
        var request = await db.ContactCorrectionRequests.FirstOrDefaultAsync(r => r.Id == requestId && r.Status == ContactCorrectionStatus.Pending);
        if (request is null)
        {
            return false;
        }

        request.Status = ContactCorrectionStatus.Rejected;
        request.ReviewedByUserId = reviewerUserId;
        request.ReviewedByName = reviewerName;
        request.ReviewedAtUtc = DateTime.UtcNow;
        request.ReviewNote = reviewNote;
        await db.SaveChangesAsync();

        await audit.RecordUserActionAsync(reviewerUserId, reviewerName, "ContactCorrectionRejected", "DatabaseInstance", request.DatabaseInstanceId.ToString(), request.RequestedByName, reason: reviewNote);
        return true;
    }

    // A detached copy of the contact fields to diff, avoiding change-tracking noise from the tracked instance.
    private static DatabaseInstance Clone(DatabaseInstance instance) => new()
    {
        Id = instance.Id,
        Name = instance.Name,
        Engine = instance.Engine,
        Version = instance.Version,
        Application = instance.Application,
        Company = instance.Company,
        Department = instance.Department,
        ContactName = instance.ContactName,
        ContactEmail = instance.ContactEmail,
        ContactPhone = instance.ContactPhone,
        OwnerTeamId = instance.OwnerTeamId,
        BackupContactName = instance.BackupContactName,
        BackupContactEmail = instance.BackupContactEmail,
        BackupContactPhone = instance.BackupContactPhone,
        Region = instance.Region,
        Cluster = instance.Cluster,
        ReplicaSet = instance.ReplicaSet,
        Role = instance.Role,
        Environment = instance.Environment,
        DatabaseIp = instance.DatabaseIp,
        ServerIp = instance.ServerIp,
        ExpectedBackupIntervalHours = instance.ExpectedBackupIntervalHours,
        BackupFileLocation = instance.BackupFileLocation,
        BackupFileNamePatterns = instance.BackupFileNamePatterns,
        RetentionPolicyDays = instance.RetentionPolicyDays,
        Tags = instance.Tags,
        RunbookUrl = instance.RunbookUrl,
        Notes = instance.Notes,
    };
}
