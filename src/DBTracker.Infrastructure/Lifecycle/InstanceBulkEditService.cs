using DBTracker.Core.Entities;
using DBTracker.Core.Services;
using DBTracker.Infrastructure.Audit;
using Microsoft.EntityFrameworkCore;

namespace DBTracker.Infrastructure.Lifecycle;

public record BulkEditApplyResult(int AppliedCount, int SkippedArchivedCount, IReadOnlyList<string> ValidationErrors);

/// <summary>
/// Applies a reviewed bulk owner/tag/retention edit (U17) to a set of instances. Permission is
/// re-checked by the caller's own [Authorize(Roles=Roles.CanEditPolicy)] on the page at request
/// time (every call here happens inside that same request); "conflicts" are re-checked here by
/// re-reading each instance fresh immediately before writing rather than trusting the preview's
/// now-possibly-stale snapshot — an instance archived since the preview was built is skipped rather
/// than silently un-archived by a bulk write that was never about archiving. Bulk edit deliberately
/// has no path to set IsArchived — archiving stays a separate, individually-confirmed action (U14).
/// </summary>
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
