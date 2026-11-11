using DBTracker.Core.Entities;
using DBTracker.Core.Services;
using DBTracker.Infrastructure.Audit;
using Microsoft.EntityFrameworkCore;

namespace DBTracker.Infrastructure.Lifecycle;

public record BulkEditApplyResult(int AppliedCount, int SkippedArchivedCount, IReadOnlyList<string> ValidationErrors);

/// <summary>Applies a reviewed bulk owner/tag/retention edit. Each instance is re-read before writing, and one archived since the preview is skipped.</summary>
/// <remarks>Permission is enforced by the page's CanEditPolicy authorization. Bulk edit cannot set IsArchived.</remarks>
public class InstanceBulkEditService(IDbContextFactory<DBTrackerDbContext> factory, AuditRecorder audit)
{
    public async Task<IReadOnlyList<BulkEditRowPreview>> PreviewAsync(IReadOnlyList<int> instanceIds, BulkEditRequest request)
    {
        using var db = await factory.CreateDbContextAsync();
        var instances = await db.DatabaseInstances.AsNoTracking().Where(i => instanceIds.Contains(i.Id)).ToListAsync();
        return BulkEditPreviewBuilder.Preview(instances, request);
    }

    public async Task<BulkEditApplyResult> ApplyAsync(IReadOnlyList<int> instanceIds, BulkEditRequest request, string actorId, string actorName)
    {
        using var db = await factory.CreateDbContextAsync();
        var instances = await db.DatabaseInstances.Where(i => instanceIds.Contains(i.Id)).ToListAsync();

        var applied = 0;
        var skippedArchived = 0;
        var now = DateTime.UtcNow;

        foreach (var instance in instances)
        {
            if (instance.IsArchived)
            {
                skippedArchived++;
                continue;
            }

            var before = Snapshot(instance);
            BulkEditPreviewBuilder.Apply(instance, request);
            instance.UpdatedAtUtc = now;
            applied++;

            await audit.RecordUserActionAsync(actorId, actorName, "InstanceBulkEdited", "DatabaseInstance", instance.Id.ToString(), instance.Name,
                reason: "Bulk edit", beforeJson: before, afterJson: Snapshot(instance));
        }

        await db.SaveChangesAsync();

        return new BulkEditApplyResult(applied, skippedArchived, []);
    }

    private static string Snapshot(DatabaseInstance i) =>
        System.Text.Json.JsonSerializer.Serialize(new { i.Company, i.Department, i.ContactName, i.ContactEmail, i.Tags, i.RetentionPolicyDays });
}
