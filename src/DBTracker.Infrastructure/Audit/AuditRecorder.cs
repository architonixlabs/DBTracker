using DBTracker.Core.Entities;
using DBTracker.Core.Enums;
using DBTracker.Core.Services;
using Microsoft.EntityFrameworkCore;

namespace DBTracker.Infrastructure.Audit;

/// <summary>
/// The single write path into the AuditEvent table (U19) — every caller (instance create/edit,
/// archive/unarchive, maintenance, CSV import/bulk edit, account changes, evidence corrections)
/// goes through here rather than writing the entity directly, so "append-only" is enforced in one
/// place: there is deliberately no Update/Delete method on this class.
///
/// Retention: events are kept indefinitely today — there is no automatic purge/archival job. If
/// this table's growth becomes a concern, add a time-boxed cleanup (e.g. keep N years) as an
/// explicit, documented policy rather than letting it grow forever by default.
///
/// Uses its own short-lived DbContext via the factory (same isolation pattern as NotificationService
/// and DeviceLinkService elsewhere in this app) so recording an audit event never races a caller's
/// own ambient scoped DbContext mid-SaveChanges.
/// </summary>
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
