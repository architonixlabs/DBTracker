using DBTracker.Core.Entities;
using DBTracker.Core.Enums;
using DBTracker.Core.Services;
using Microsoft.EntityFrameworkCore;

namespace DBTracker.Infrastructure.Audit;

/// <summary>The only write path into AuditEvent. It has no Update or Delete, which is what keeps the log append-only.</summary>
/// <remarks>Retention: events are kept indefinitely; there is no purge job, and the /audit page states this. Uses its own short-lived DbContext so recording never races the caller's scoped context mid-SaveChanges.</remarks>
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

    /// <param name="includeCommercialReasons">False for roles outside the commercial-evidence policy: search then ignores the reason text of
    /// cost-estimate events, so searching for an amount can't reveal a row whose figures are hidden.</param>
    /// <param name="take">Rows to return, newest first. Null returns every matching row (used by the export).</param>
    /// <param name="fromUtc">Inclusive lower bound on the event time.</param>
    /// <param name="toUtc">Inclusive upper bound on the event time.</param>
    public async Task<IReadOnlyList<AuditEvent>> SearchAsync(string? search, AuditActorType? actorType, string? targetType, int? take = 200, bool includeCommercialReasons = true,
        DateTime? fromUtc = null, DateTime? toUtc = null, CancellationToken ct = default)
    {
        using var db = await factory.CreateDbContextAsync(ct);
        var query = db.AuditEvents.AsNoTracking().OrderByDescending(e => e.TimestampUtc).ThenByDescending(e => e.Id).AsQueryable();

        if (fromUtc is { } from)
        {
            query = query.Where(e => e.TimestampUtc >= from);
        }
        if (toUtc is { } to)
        {
            query = query.Where(e => e.TimestampUtc <= to);
        }

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
                (e.Reason.Contains(s) && (includeCommercialReasons || e.Action != "EditionCostEstimateRecorded")));
        }

        return take is { } n ? await query.Take(n).ToListAsync(ct) : await query.ToListAsync(ct);
    }

    /// <summary>Events with one of the given actions in a time window, oldest first. Used by Trends to show policy changes.</summary>
    public async Task<IReadOnlyList<AuditEvent>> ListByActionAsync(IReadOnlyCollection<string> actions, DateTime fromUtc, DateTime toUtc, CancellationToken ct = default)
    {
        using var db = await factory.CreateDbContextAsync(ct);
        return await db.AuditEvents.AsNoTracking()
            .Where(e => actions.Contains(e.Action) && e.TimestampUtc >= fromUtc && e.TimestampUtc <= toUtc)
            .OrderBy(e => e.TimestampUtc)
            .ToListAsync(ct);
    }
}
