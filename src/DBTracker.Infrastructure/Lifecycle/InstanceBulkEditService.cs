using DBTracker.Core.Entities;
using DBTracker.Core.Services;
using DBTracker.Infrastructure.Identity;
using Microsoft.EntityFrameworkCore;

namespace DBTracker.Infrastructure.Lifecycle;

/// <summary>A preview plus the version of each previewed instance, so apply can tell which ones changed in between.</summary>
public record BulkEditPreview(IReadOnlyList<BulkEditRowPreview> Rows, IReadOnlyDictionary<int, byte[]> Versions);

public record BulkEditApplyResult(
    bool Saved,
    int AppliedCount,
    int SkippedArchivedCount,
    IReadOnlyList<string> SkippedChangedSincePreview,
    IReadOnlyList<string> ValidationErrors,
    string BatchId);

/// <summary>Applies a reviewed bulk owner/tag/retention edit, all or nothing. Bulk edit cannot set IsArchived.</summary>
/// <remarks>Apply rechecks the actor's role and active status in the database, validates every changed instance with
/// InstanceValidator before saving any, skips instances changed or archived since the preview, and writes the audit rows
/// in the same SaveChanges as the edits under one batch id, so a failed save leaves no audit trail of changes that never happened.</remarks>
public class InstanceBulkEditService(IDbContextFactory<DBTrackerDbContext> factory)
{
    public async Task<BulkEditPreview> PreviewAsync(IReadOnlyList<int> instanceIds, BulkEditRequest request)
    {
        using var db = await factory.CreateDbContextAsync();
        var instances = await db.DatabaseInstances.AsNoTracking().Where(i => instanceIds.Contains(i.Id)).ToListAsync();
        var rows = BulkEditPreviewBuilder.Preview(instances, request);
        var previewedIds = rows.Select(r => r.InstanceId).ToHashSet();
        var versions = instances.Where(i => previewedIds.Contains(i.Id)).ToDictionary(i => i.Id, i => i.RowVersion);
        return new BulkEditPreview(rows, versions);
    }

    public async Task<BulkEditApplyResult> ApplyAsync(BulkEditPreview preview, BulkEditRequest request, string actorId, string actorName)
    {
        var batchId = Guid.NewGuid().ToString("N")[..8];
        using var db = await factory.CreateDbContextAsync();

        if (!await CanEditAsync(db, actorId))
        {
            return Refused(batchId, "Your account can no longer edit instances. Nothing was saved.");
        }

        var ids = preview.Versions.Keys.ToList();
        var instances = await db.DatabaseInstances.Where(i => ids.Contains(i.Id)).ToListAsync();

        var skippedArchived = 0;
        var skippedChanged = new List<string>();
        var toApply = new List<DatabaseInstance>();
        foreach (var instance in instances)
        {
            if (instance.IsArchived)
            {
                skippedArchived++;
            }
            else if (!instance.RowVersion.AsSpan().SequenceEqual(preview.Versions[instance.Id]))
            {
                skippedChanged.Add(instance.Name);
            }
            else
            {
                toApply.Add(instance);
            }
        }

        // Only the fields this batch changes can block it; an unrelated, older problem on a record is fixed on its own edit page.
        var editedFields = EditedFields(request);
        var now = DateTime.UtcNow;
        var validationErrors = new List<string>();
        foreach (var instance in toApply)
        {
            var before = Snapshot(instance);
            BulkEditPreviewBuilder.Apply(instance, request);
            instance.UpdatedAtUtc = now;

            validationErrors.AddRange(InstanceValidator.Validate(instance)
                .Where(e => editedFields.Contains(e.Field))
                .Select(e => $"{instance.Name}: {e.Message}"));

            db.AuditEvents.Add(AuditEventFactory.ForUser(actorId, actorName, "InstanceBulkEdited", "DatabaseInstance", instance.Id.ToString(), instance.Name, now,
                reason: $"Bulk edit batch {batchId} ({toApply.Count} instance(s))", beforeJson: before, afterJson: Snapshot(instance)));
        }

        if (validationErrors.Count > 0)
        {
            return new BulkEditApplyResult(false, 0, skippedArchived, skippedChanged, validationErrors.Distinct().ToList(), batchId);
        }

        if (toApply.Count > 0)
        {
            await db.SaveChangesAsync();
        }

        return new BulkEditApplyResult(true, toApply.Count, skippedArchived, skippedChanged, [], batchId);
    }

    private static async Task<bool> CanEditAsync(DBTrackerDbContext db, string actorId)
    {
        var user = await db.Users.AsNoTracking().FirstOrDefaultAsync(u => u.Id == actorId);
        if (user is null || !user.IsActive)
        {
            return false;
        }

        var allowedRoles = Roles.CanEditPolicy.Split(',');
        return await db.UserRoles.AsNoTracking()
            .Where(ur => ur.UserId == actorId)
            .Join(db.Roles, ur => ur.RoleId, r => r.Id, (ur, r) => r.Name)
            .AnyAsync(name => allowedRoles.Contains(name!));
    }

    private static HashSet<string> EditedFields(BulkEditRequest request)
    {
        var fields = new HashSet<string>();
        if (request.SetOwner)
        {
            fields.UnionWith([nameof(DatabaseInstance.Company), nameof(DatabaseInstance.Department), nameof(DatabaseInstance.ContactName), nameof(DatabaseInstance.ContactEmail)]);
        }
        if (request.SetTags)
        {
            fields.Add(nameof(DatabaseInstance.Tags));
        }
        if (request.SetRetention)
        {
            fields.Add(nameof(DatabaseInstance.RetentionPolicyDays));
        }
        return fields;
    }

    private static BulkEditApplyResult Refused(string batchId, string message) =>
        new(false, 0, 0, [], [message], batchId);

    private static string Snapshot(DatabaseInstance i) =>
        System.Text.Json.JsonSerializer.Serialize(new { i.Company, i.Department, i.ContactName, i.ContactEmail, i.Tags, i.RetentionPolicyDays });
}
