using DBTracker.Core.Entities;
using DBTracker.Core.Enums;
using DBTracker.Core.Services;
using DBTracker.Infrastructure.Audit;
using Microsoft.EntityFrameworkCore;

namespace DBTracker.Infrastructure.ChangeManagement;

/// <summary>Readiness exceptions (TargetType + TargetId + WaivedCheckReference). Approval checks the approver's role and, when required, that a different person prepared it.</summary>
/// <remarks>Expiry is never written as a status; the sweep only sends a one-time notice, and gates check IsActive live.</remarks>
public class ReadinessExceptionService(IDbContextFactory<DBTrackerDbContext> factory, AuditRecorder audit)
{
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

    /// <summary>The renewal/rejection chain for one exception, oldest first.</summary>
    public async Task<List<ReadinessException>> GetHistoryChainAsync(int id)
    {
        using var db = await factory.CreateDbContextAsync();
        var all = await db.ReadinessExceptions.AsNoTracking().ToListAsync();
        var chain = new List<ReadinessException>();
        var current = all.FirstOrDefault(e => e.Id == id);
        while (current is not null)
        {
            chain.Insert(0, current);
            current = current.RenewalOfExceptionId is { } prevId ? all.FirstOrDefault(e => e.Id == prevId) : null;
        }
        return chain;
    }

    public async Task<ReadinessException> CreateAsync(ReadinessException exception, string preparedByUserId, string preparedByUserName)
    {
        using var db = await factory.CreateDbContextAsync();
        exception.PreparedByUserId = preparedByUserId;
        exception.PreparedByUserName = preparedByUserName;
        exception.CreatedBy = preparedByUserName;
        exception.CreatedAtUtc = DateTime.UtcNow;
        exception.Status = ReadinessExceptionStatus.PendingApproval;
        db.ReadinessExceptions.Add(exception);
        await db.SaveChangesAsync();

        await audit.RecordUserActionAsync(preparedByUserId, preparedByUserName, "ReadinessExceptionCreated", exception.TargetType, exception.TargetId.ToString(), exception.WaivedCheckReference,
            reason: exception.Reason);
        return exception;
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
            return (false, "Not authorized to decide this exception — permitted approver role doesn't match.");
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

    /// <summary>Revokes an approved exception early; whatever it covered is blocked again immediately.</summary>
    public async Task<bool> RevokeAsync(int id, string revokedBy, string reason)
    {
        using var db = await factory.CreateDbContextAsync();
        var exception = await db.ReadinessExceptions.FirstOrDefaultAsync(e => e.Id == id);
        if (exception is null || exception.Status != ReadinessExceptionStatus.Approved)
        {
            return false;
        }

        exception.Status = ReadinessExceptionStatus.Revoked;
        exception.RevokedAtUtc = DateTime.UtcNow;
        exception.RevokedBy = revokedBy;
        exception.RevokedReason = reason;
        await db.SaveChangesAsync();

        await audit.RecordUserActionAsync("", revokedBy, "ReadinessExceptionRevoked", exception.TargetType, exception.TargetId.ToString(), exception.WaivedCheckReference, reason: reason);
        return true;
    }

    /// <summary>Creates an exception renewing a rejected, expired or revoked one, linked via RenewalOfExceptionId.</summary>
    public async Task<ReadinessException> RenewAsync(int originalId, ReadinessException renewal, string preparedByUserId, string preparedByUserName)
    {
        renewal.RenewalOfExceptionId = originalId;
        return await CreateAsync(renewal, preparedByUserId, preparedByUserName);
    }

    /// <summary>One sweep: notify the owner once for each Approved exception that has newly expired. Shared by the hosted service and the manual admin action.</summary>
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
            var message = $"Readiness exception expired: {exception.TargetType} #{exception.TargetId} — \"{exception.WaivedCheckReference}\" is no longer waived. Owner: {ownerLabel}. The underlying action is reopened.";
            db.Notifications.Add(new Notification
            {
                DatabaseInstanceId = null,
                InstanceName = $"{exception.TargetType} #{exception.TargetId}",
                Message = message,
                Severity = NotificationSeverity.Warning,
                CreatedAtUtc = now
            });
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
}
