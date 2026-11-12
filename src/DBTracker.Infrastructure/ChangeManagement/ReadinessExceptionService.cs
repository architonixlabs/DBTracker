using DBTracker.Core.Entities;
using DBTracker.Core.Enums;
using DBTracker.Core.Services;
using DBTracker.Infrastructure.Audit;
using DBTracker.Infrastructure.Identity;
using DBTracker.Infrastructure.Notifications;
using Microsoft.EntityFrameworkCore;

namespace DBTracker.Infrastructure.ChangeManagement;

/// <summary>One record an exception can be raised against, for the target picker.</summary>
public record ExceptionTargetOption(int Id, string Label);

/// <summary>Readiness exceptions (TargetType + TargetId + WaivedCheckReference). Approval checks the approver's role and, when required, that a different person prepared it.</summary>
/// <remarks>Expiry is never written as a status; the sweep only sends a one-time notice, and gates check IsActive live. Expiry and revocation
/// notices go to the accountable owner through OwnerNoticeSender.</remarks>
public class ReadinessExceptionService(IDbContextFactory<DBTrackerDbContext> factory, AuditRecorder audit, OwnerNoticeSender ownerNotices)
{
    private static readonly string[] EditorRoles = [Roles.Admin, Roles.Dba];

    public async Task<List<ReadinessException>> ListAsync(string? targetType = null, int? targetId = null)
    {
        using var db = await factory.CreateDbContextAsync();
        var query = db.ReadinessExceptions.AsNoTracking().Include(e => e.AccountableOwnerTeam).AsQueryable();
        if (!string.IsNullOrWhiteSpace(targetType))
        {
            query = query.Where(e => e.TargetType == targetType);
        }
        if (targetId is { } id)
        {
            query = query.Where(e => e.TargetId == id);
        }
        return await query.OrderByDescending(e => e.CreatedAtUtc).ToListAsync();
    }

    public async Task<ReadinessException?> GetAsync(int id)
    {
        using var db = await factory.CreateDbContextAsync();
        return await db.ReadinessExceptions.AsNoTracking().Include(e => e.AccountableOwnerTeam).Include(e => e.RenewalOfException).FirstOrDefaultAsync(e => e.Id == id);
    }

    /// <summary>The whole renewal/rejection chain an exception belongs to, oldest first, including any later renewals of it.</summary>
    public async Task<List<ReadinessException>> GetHistoryChainAsync(int id)
    {
        using var db = await factory.CreateDbContextAsync();
        var all = await db.ReadinessExceptions.AsNoTracking().ToListAsync();
        var byId = all.ToDictionary(e => e.Id);
        if (!byId.TryGetValue(id, out var current))
        {
            return [];
        }

        var root = current;
        var guard = 0;
        while (root.RenewalOfExceptionId is { } prevId && byId.TryGetValue(prevId, out var previous) && guard++ < 1000)
        {
            root = previous;
        }

        var chain = new List<ReadinessException> { root };
        var next = all.FirstOrDefault(e => e.RenewalOfExceptionId == root.Id);
        while (next is not null && chain.Count < 1000)
        {
            chain.Add(next);
            var nextId = next.Id;
            next = all.FirstOrDefault(e => e.RenewalOfExceptionId == nextId);
        }
        return chain;
    }

    /// <summary>Records the target type picker can offer, newest first, so a target is chosen from what exists rather than typed as an id.</summary>
    public async Task<List<ExceptionTargetOption>> ListTargetOptionsAsync(string targetType)
    {
        using var db = await factory.CreateDbContextAsync();
        return targetType switch
        {
            ReadinessExceptionTargets.DatabaseInstance => await db.DatabaseInstances.AsNoTracking().Where(i => !i.IsArchived)
                .OrderBy(i => i.Name).Select(i => new ExceptionTargetOption(i.Id, i.Name)).ToListAsync(),
            ReadinessExceptionTargets.MigrationCampaign => (await db.MigrationCampaigns.AsNoTracking()
                    .OrderByDescending(c => c.UpdatedAtUtc).Select(c => new { c.Id, c.Name, c.State }).ToListAsync())
                .Select(c => new ExceptionTargetOption(c.Id, $"{c.Name} ({c.State})")).ToList(),
            ReadinessExceptionTargets.ChangeWindow => (await db.ChangeWindows.AsNoTracking()
                    .Where(w => w.Status != ChangeWindowStatus.Cancelled)
                    .OrderByDescending(w => w.PlannedStartUtc)
                    .Select(w => new { w.Id, w.Title, CampaignName = w.MigrationCampaign!.Name }).ToListAsync())
                .Select(w => new ExceptionTargetOption(w.Id, $"{w.Title} ({w.CampaignName})")).ToList(),
            ReadinessExceptionTargets.RetirementChecklist => (await db.RetirementChecklists.AsNoTracking()
                    .Select(r => new { r.Id, InstanceName = r.DatabaseInstance!.Name }).ToListAsync())
                .Select(r => new ExceptionTargetOption(r.Id, $"Retirement of {r.InstanceName}")).ToList(),
            _ => []
        };
    }

    /// <summary>A readable name for each exception's target, keyed by (type, id). Missing targets read "#id (not found)".</summary>
    public async Task<Dictionary<(string, int), string>> DescribeTargetsAsync(IEnumerable<ReadinessException> exceptions)
    {
        var result = new Dictionary<(string, int), string>();
        foreach (var group in exceptions.GroupBy(e => e.TargetType))
        {
            var options = (await ListTargetOptionsAsync(group.Key)).ToDictionary(o => o.Id, o => o.Label);
            foreach (var e in group)
            {
                result[(e.TargetType, e.TargetId)] = options.TryGetValue(e.TargetId, out var label) ? label : $"#{e.TargetId} (not found)";
            }
        }
        return result;
    }

    private async Task<bool> TargetExistsAsync(string targetType, int targetId) =>
        (await ListTargetOptionsAsync(targetType)).Any(o => o.Id == targetId);

    public async Task<(bool Ok, string Reason, ReadinessException? Exception)> CreateAsync(ReadinessException exception, string preparedByUserId, string preparedByUserName,
        IReadOnlyCollection<string> actorRoles)
    {
        if (!actorRoles.Any(EditorRoles.Contains))
        {
            return (false, "Only Admin and DBA users can record exceptions.", null);
        }
        if (!ReadinessExceptionTargets.All.Contains(exception.TargetType))
        {
            return (false, "Choose what the exception applies to.", null);
        }
        if (!await TargetExistsAsync(exception.TargetType, exception.TargetId))
        {
            return (false, $"Choose an existing {ReadinessExceptionTargets.Describe(exception.TargetType).ToLowerInvariant()}.", null);
        }
        if (string.IsNullOrWhiteSpace(exception.WaivedCheckReference) || string.IsNullOrWhiteSpace(exception.Reason))
        {
            return (false, "Say which check is waived and why.", null);
        }
        if (ReadinessExceptionEvaluator.ValidateExpiry(exception.ExpiresAtUtc, DateTime.UtcNow) is { } expiryError)
        {
            return (false, expiryError, null);
        }

        using var db = await factory.CreateDbContextAsync();
        exception.PreparedByUserId = preparedByUserId;
        exception.PreparedByUserName = preparedByUserName;
        exception.CreatedBy = preparedByUserName;
        exception.CreatedAtUtc = DateTime.UtcNow;
        exception.Status = ReadinessExceptionStatus.PendingApproval;
        exception.AccountableOwnerEmail = exception.AccountableOwnerEmail.Trim();
        db.ReadinessExceptions.Add(exception);
        await db.SaveChangesAsync();

        await audit.RecordUserActionAsync(preparedByUserId, preparedByUserName,
            exception.RenewalOfExceptionId is not null ? "ReadinessExceptionRenewalRequested" : "ReadinessExceptionCreated",
            exception.TargetType, exception.TargetId.ToString(), exception.WaivedCheckReference,
            reason: exception.RenewalOfExceptionId is { } prev ? $"Renews #{prev} until {exception.ExpiresAtUtc:yyyy-MM-dd}. {exception.Reason}" : exception.Reason);
        return (true, "", exception);
    }

    /// <summary>Approve or reject a pending exception. Refuses (false, reason) when the caller's role doesn't authorize PermittedApproverRole, or when separation of duties requires a different preparer/approver.</summary>
    public async Task<(bool Ok, string Reason)> DecideAsync(int id, bool approve, string approverUserId, string approverUserName,
        IReadOnlyCollection<string> approverRoles, string decisionNotes)
    {
        using var db = await factory.CreateDbContextAsync();
        var exception = await db.ReadinessExceptions.FirstOrDefaultAsync(e => e.Id == id);
        if (exception is null)
        {
            return (false, "Exception not found.");
        }
        if (exception.Status != ReadinessExceptionStatus.PendingApproval)
        {
            return (false, $"Already {exception.Status}.");
        }
        if (!ExceptionApprovalAuthorizer.IsAuthorized(exception.PermittedApproverRole, approverRoles))
        {
            return (false, "Not authorized to decide this exception: the permitted approver role doesn't match.");
        }
        if (approve && !ReadinessExceptionEvaluator.CanApprove(exception, approverUserId))
        {
            return (false, "This exception requires a different approver from whoever prepared it.");
        }

        exception.Status = approve ? ReadinessExceptionStatus.Approved : ReadinessExceptionStatus.Rejected;
        exception.ApprovedByUserId = approverUserId;
        exception.ApprovedByUserName = approverUserName;
        exception.DecidedAtUtc = DateTime.UtcNow;
        exception.DecisionNotes = decisionNotes;
        await db.SaveChangesAsync();

        await audit.RecordUserActionAsync(approverUserId, approverUserName, approve ? "ReadinessExceptionApproved" : "ReadinessExceptionRejected",
            exception.TargetType, exception.TargetId.ToString(), exception.WaivedCheckReference, reason: decisionNotes);
        return (true, "");
    }

    /// <summary>Revokes an approved exception early; whatever it covered is blocked again immediately, and the accountable owner is told.</summary>
    public async Task<(bool Ok, string Reason)> RevokeAsync(int id, string revokedBy, string reason, IReadOnlyCollection<string> actorRoles)
    {
        if (!actorRoles.Any(EditorRoles.Contains))
        {
            return (false, "Only Admin and DBA users can revoke exceptions.");
        }
        if (string.IsNullOrWhiteSpace(reason))
        {
            return (false, "Say why the exception is being revoked.");
        }

        using var db = await factory.CreateDbContextAsync();
        var exception = await db.ReadinessExceptions.FirstOrDefaultAsync(e => e.Id == id);
        if (exception is null || exception.Status != ReadinessExceptionStatus.Approved)
        {
            return (false, "Only an approved exception can be revoked.");
        }

        exception.Status = ReadinessExceptionStatus.Revoked;
        exception.RevokedAtUtc = DateTime.UtcNow;
        exception.RevokedBy = revokedBy;
        exception.RevokedReason = reason.Trim();
        await db.SaveChangesAsync();

        await audit.RecordUserActionAsync("", revokedBy, "ReadinessExceptionRevoked", exception.TargetType, exception.TargetId.ToString(), exception.WaivedCheckReference, reason: reason);

        var routed = await NotifyOwnerAsync(db, exception, "revoked",
            $"Readiness exception revoked by {revokedBy}: {await TargetLabelAsync(exception)}: \"{exception.WaivedCheckReference}\" is no longer waived ({exception.RevokedReason}). The waived action is open again.",
            NotificationSeverity.Warning);
        await audit.RecordUserActionAsync("", revokedBy, "ReadinessExceptionOwnerNotified", exception.TargetType, exception.TargetId.ToString(), exception.WaivedCheckReference,
            reason: $"Revocation notice sent to {routed.RoutedTo}.");
        return (true, routed.RoutedTo);
    }

    /// <summary>Creates a pending exception renewing a rejected, expired or revoked one, linked via RenewalOfExceptionId so the history is kept.
    /// The caller chooses the new expiry; it is never defaulted.</summary>
    public async Task<(bool Ok, string Reason, ReadinessException? Exception)> RenewAsync(int originalId, DateTime newExpiresAtUtc, DateTime? reviewDate, string reason,
        string preparedByUserId, string preparedByUserName, IReadOnlyCollection<string> actorRoles)
    {
        using var db = await factory.CreateDbContextAsync();
        var all = await db.ReadinessExceptions.AsNoTracking().ToListAsync();
        var original = all.FirstOrDefault(e => e.Id == originalId);
        if (original is null)
        {
            return (false, "Exception not found.", null);
        }

        var effective = ReadinessExceptionEvaluator.EffectiveStatus(original, DateTime.UtcNow);
        if (effective is not (ReadinessExceptionEffectiveStatus.Expired or ReadinessExceptionEffectiveStatus.Rejected or ReadinessExceptionEffectiveStatus.Revoked))
        {
            return (false, $"Only an expired, rejected or revoked exception can be renewed; this one is {effective}.", null);
        }
        if (!ReadinessExceptionEvaluator.IsLatestInChain(original, all))
        {
            return (false, "This exception has already been renewed. Renew the latest one in its history instead.", null);
        }

        var renewal = new ReadinessException
        {
            TargetType = original.TargetType,
            TargetId = original.TargetId,
            WaivedCheckReference = original.WaivedCheckReference,
            Reason = string.IsNullOrWhiteSpace(reason) ? original.Reason : reason.Trim(),
            CompensatingControls = original.CompensatingControls,
            AccountableOwnerTeamId = original.AccountableOwnerTeamId,
            AccountableOwnerName = original.AccountableOwnerName,
            AccountableOwnerEmail = original.AccountableOwnerEmail,
            PermittedApproverRole = original.PermittedApproverRole,
            RequiresSeparateApprover = original.RequiresSeparateApprover,
            EvidenceUrl = original.EvidenceUrl,
            EvidenceNotes = original.EvidenceNotes,
            ExpiresAtUtc = DateTime.SpecifyKind(newExpiresAtUtc, DateTimeKind.Utc),
            ReviewDate = reviewDate,
            RenewalOfExceptionId = originalId
        };
        return await CreateAsync(renewal, preparedByUserId, preparedByUserName, actorRoles);
    }

    /// <summary>One sweep: notify the accountable owner once for each Approved exception that has newly expired. Shared by the hosted service and the manual admin action.</summary>
    public async Task<int> EvaluateAndNotifyExpiringAsync(CancellationToken ct = default)
    {
        using var db = await factory.CreateDbContextAsync(ct);
        var now = DateTime.UtcNow;
        var candidates = await db.ReadinessExceptions
            .Where(e => e.Status == ReadinessExceptionStatus.Approved && e.ExpiresAtUtc <= now && e.ExpiryNotifiedAtUtc == null)
            .ToListAsync(ct);

        var notifiedCount = 0;
        foreach (var exception in candidates)
        {
            var ownerLabel = !string.IsNullOrWhiteSpace(exception.AccountableOwnerName) ? exception.AccountableOwnerName : "accountable owner not recorded";
            await NotifyOwnerAsync(db, exception, "expired",
                $"Readiness exception expired: {await TargetLabelAsync(exception)}: \"{exception.WaivedCheckReference}\" is no longer waived. Owner: {ownerLabel}. The underlying action is open again.",
                NotificationSeverity.Warning, ct);
            exception.ExpiryNotifiedAtUtc = now;
            notifiedCount++;
        }

        await db.SaveChangesAsync(ct);
        if (notifiedCount > 0)
        {
            await audit.RecordSystemActionAsync("ReadinessExceptionExpiryCheck", "ReadinessExceptionsExpired", "ReadinessException", "", $"{notifiedCount} exception(s)", ct: ct);
        }
        return notifiedCount;
    }

    private async Task<string> TargetLabelAsync(ReadinessException exception)
    {
        var labels = await DescribeTargetsAsync([exception]);
        return $"{ReadinessExceptionTargets.Describe(exception.TargetType)} {labels[(exception.TargetType, exception.TargetId)]}";
    }

    private async Task<OwnerNoticeRecipients> NotifyOwnerAsync(DBTrackerDbContext db, ReadinessException exception, string what, string message, NotificationSeverity severity,
        CancellationToken ct = default)
    {
        var instance = exception.TargetType == ReadinessExceptionTargets.DatabaseInstance
            ? await db.DatabaseInstances.AsNoTracking().FirstOrDefaultAsync(i => i.Id == exception.TargetId, ct)
            : null;
        return await ownerNotices.SendAsync(new OwnerNotice(
            $"DBTracker: readiness exception {what}",
            message,
            severity,
            exception.AccountableOwnerEmail,
            exception.AccountableOwnerTeamId,
            instance,
            $"{ReadinessExceptionTargets.Describe(exception.TargetType)} #{exception.TargetId}"), ct);
    }
}
