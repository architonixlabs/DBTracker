using DBTracker.Core.Entities;
using DBTracker.Core.Enums;
using DBTracker.Core.Services;
using Microsoft.EntityFrameworkCore;

namespace DBTracker.Infrastructure.Audit;

/// <summary>The only write path into AuditEvent. It has no Update or Delete, which is what keeps the log append-only.</summary>
/// <remarks>Events are kept indefinitely; there is no purge job. Uses its own short-lived DbContext so recording never races the caller's scoped context mid-SaveChanges.</remarks>
public class AuditRecorder(IDbContextFactory<DBTrackerDbContext> factory)
{
    public async Task RecordAsync(AuditEvent evt, CancellationToken ct = default)
    {
        using var db = await factory.CreateDbContextAsync(ct);
        db.AuditEvents.Add(evt);
        await db.SaveChangesAsync(ct);
    }

    public Task RecordUserActionAsync(string userId, string userName, string action, string targetType, string targetId, string targetName, string reason = "", string? beforeJson = null, string? afterJson = null, CancellationToken ct = default) =>
        RecordAsync(AuditEventFactory.ForUser(userId, userName, action, targetType, targetId, targetName, DateTime.UtcNow, reason, beforeJson, afterJson), ct);

    public Task RecordSystemActionAsync(string workerName, string action, string targetType, string targetId, string targetName, string reason = "", CancellationToken ct = default) =>
        RecordAsync(AuditEventFactory.ForSystem(workerName, action, targetType, targetId, targetName, DateTime.UtcNow, reason), ct);

    public async Task<IReadOnlyList<AuditEvent>> SearchAsync(string? search, AuditActorType? actorType, string? targetType, int take = 200, CancellationToken ct = default)
    {
        using var db = await factory.CreateDbContextAsync(ct);
        var query = db.AuditEvents.AsNoTracking().OrderByDescending(e => e.TimestampUtc).AsQueryable();

        if (actorType is { } at)
        {
            query = query.Where(e => e.ActorType == at);
        }
        if (!string.IsNullOrWhiteSpace(targetType))
        {
            query = query.Where(e => e.TargetType == targetType);
        }
        if (!string.IsNullOrWhiteSpace(search))
        {
            var s = search.Trim();
            query = query.Where(e =>
                e.ActorName.Contains(s) ||
                e.Action.Contains(s) ||
                e.TargetName.Contains(s) ||
                e.Reason.Contains(s));
        }

        return await query.Take(take).ToListAsync(ct);
    }
}
