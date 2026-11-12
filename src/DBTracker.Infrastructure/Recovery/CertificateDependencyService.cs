using DBTracker.Core.Entities;
using DBTracker.Core.Enums;
using DBTracker.Core.Services;
using DBTracker.Infrastructure.Audit;
using DBTracker.Infrastructure.Identity;
using DBTracker.Infrastructure.Notifications;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;

namespace DBTracker.Infrastructure.Recovery;

public record CertificateRow(CertificateDependency Certificate, CertificateExpiryStatus ExpiryStatus, KeyRotationStatus RotationStatus);

/// <summary>Certificate and recovery-key reminders. Free-text fields go through SecretLeakGuard before saving, so a pasted secret never reaches
/// the database. Renewals and rotations are recorded facts about work done elsewhere; nothing here issues, renews or rotates anything.</summary>
public class CertificateDependencyService(
    IDbContextFactory<DBTrackerDbContext> factory,
    AuditRecorder audit,
    OwnerNoticeSender ownerNotices,
    IConfiguration configuration)
{
    private static readonly string[] EditorRoles = [Roles.Admin, Roles.Dba];

    public async Task<List<CertificateRow>> ListForInstanceAsync(int instanceId)
    {
        using var db = await factory.CreateDbContextAsync();
        var certs = await db.CertificateDependencies.AsNoTracking()
            .Include(c => c.OwnerTeam)
            .Include(c => c.History)
            .Where(c => c.DatabaseInstanceId == instanceId)
            .OrderBy(c => c.Label)
            .ToListAsync();

        var now = DateTime.UtcNow;
        return certs.Select(c => new CertificateRow(
            c,
            CertificateExpiryEvaluator.Classify(c.ExpiresAtUtc, now),
            KeyRotationEvaluator.Classify(c.LastRotatedAtUtc, c.RotationIntervalDays, now))).ToList();
    }

    public async Task<CertificateDependency> AddAsync(CertificateDependency cert, string actorName)
    {
        cert.SecretStoreReference = SecretLeakGuard.Redact(cert.SecretStoreReference);
        cert.Notes = SecretLeakGuard.Redact(cert.Notes);
        cert.RenewalEvidenceText = SecretLeakGuard.Redact(cert.RenewalEvidenceText);
        cert.AffectedReplicasOrClientsText = SecretLeakGuard.Redact(cert.AffectedReplicasOrClientsText);
        cert.OwnerEmail = cert.OwnerEmail.Trim();

        using var db = await factory.CreateDbContextAsync();
        cert.CreatedAtUtc = DateTime.UtcNow;
        cert.UpdatedAtUtc = DateTime.UtcNow;
        cert.UpdatedBy = actorName;
        db.CertificateDependencies.Add(cert);
        await db.SaveChangesAsync();

        var instance = await db.DatabaseInstances.FirstAsync(i => i.Id == cert.DatabaseInstanceId);
        await audit.RecordUserActionAsync("", actorName, "CertificateDependencyRecorded", "DatabaseInstance", cert.DatabaseInstanceId.ToString(), instance.Name,
            reason: $"{cert.Kind}: {cert.Label}");
        return cert;
    }

    public async Task SetVerifiedAccessAsync(int certId, bool verified, string actorName)
    {
        using var db = await factory.CreateDbContextAsync();
        var cert = await db.CertificateDependencies.FirstOrDefaultAsync(c => c.Id == certId);
        if (cert is null)
        {
            return;
        }

        cert.VerifiedBackupRestoreAccess = verified;
        cert.LastAccessCheckAtUtc = DateTime.UtcNow;
        cert.LastAccessCheckBy = actorName;
        cert.UpdatedAtUtc = DateTime.UtcNow;
        cert.UpdatedBy = actorName;
        await db.SaveChangesAsync();

        var instance = await db.DatabaseInstances.FirstAsync(i => i.Id == cert.DatabaseInstanceId);
        await audit.RecordUserActionAsync("", actorName, "CertificateAccessVerified", "DatabaseInstance", cert.DatabaseInstanceId.ToString(), instance.Name,
            reason: $"{cert.Label}: verified={verified}");
    }

    /// <summary>Records a renewal: the new expiry replaces the old one, the expiry-notice thresholds start over, and a history entry keeps the
    /// previous date, the evidence and which replicas or clients were updated.</summary>
    public async Task<(bool Ok, string Error)> RecordRenewalAsync(int certId, DateTime renewedAtUtc, DateTime? newExpiresAtUtc, string evidence,
        string replicasOrClientsUpdated, string actorName, IReadOnlyCollection<string> actorRoles)
    {
        if (!actorRoles.Any(EditorRoles.Contains))
        {
            return (false, "Only Admin and DBA users can record renewals.");
        }

        using var db = await factory.CreateDbContextAsync();
        var cert = await db.CertificateDependencies.FirstOrDefaultAsync(c => c.Id == certId);
        if (cert is null)
        {
            return (false, "Certificate dependency not found.");
        }

        var now = DateTime.UtcNow;
        var error = CertificateRenewalRules.ValidateRenewal(cert.ExpiresAtUtc, newExpiresAtUtc, renewedAtUtc, now);
        if (error is not null)
        {
            return (false, error);
        }

        var safeEvidence = SecretLeakGuard.Redact(evidence.Trim());
        var safeReplicas = SecretLeakGuard.Redact(replicasOrClientsUpdated.Trim());
        db.CertificateRenewalEvents.Add(new CertificateRenewalEvent
        {
            CertificateDependencyId = cert.Id,
            EventType = CertificateEventType.Renewal,
            OccurredAtUtc = DateTime.SpecifyKind(renewedAtUtc, DateTimeKind.Utc),
            PreviousExpiresAtUtc = cert.ExpiresAtUtc,
            NewExpiresAtUtc = newExpiresAtUtc,
            Evidence = safeEvidence,
            ReplicasOrClientsUpdated = safeReplicas,
            RecordedAtUtc = now,
            RecordedBy = actorName
        });

        var previous = cert.ExpiresAtUtc;
        cert.ExpiresAtUtc = newExpiresAtUtc;
        cert.LastRenewedAtUtc = DateTime.SpecifyKind(renewedAtUtc, DateTimeKind.Utc);
        cert.RenewalEvidenceText = safeEvidence;
        cert.LastExpiryNoticeThresholdDays = null;
        cert.LastExpiryNoticeAtUtc = null;
        cert.UpdatedAtUtc = now;
        cert.UpdatedBy = actorName;
        await db.SaveChangesAsync();

        var instance = await db.DatabaseInstances.FirstAsync(i => i.Id == cert.DatabaseInstanceId);
        await audit.RecordUserActionAsync("", actorName, "CertificateRenewalRecorded", "DatabaseInstance", cert.DatabaseInstanceId.ToString(), instance.Name,
            reason: $"{cert.Label}: expiry {previous?.ToString("yyyy-MM-dd") ?? "unknown"} -> {newExpiresAtUtc:yyyy-MM-dd}" +
                    (string.IsNullOrWhiteSpace(safeReplicas) ? "" : $"; updated: {safeReplicas}"));
        return (true, "");
    }

    /// <summary>Records a key rotation with its evidence. Rotation is judged by KeyRotationEvaluator and leaves the certificate expiry as it is.</summary>
    public async Task<(bool Ok, string Error)> RecordRotationAsync(int certId, DateTime rotatedAtUtc, string evidence, string replicasOrClientsUpdated,
        string actorName, IReadOnlyCollection<string> actorRoles)
    {
        if (!actorRoles.Any(EditorRoles.Contains))
        {
            return (false, "Only Admin and DBA users can record key rotations.");
        }
        if (CertificateRenewalRules.ValidateRotation(rotatedAtUtc, DateTime.UtcNow) is { } error)
        {
            return (false, error);
        }

        using var db = await factory.CreateDbContextAsync();
        var cert = await db.CertificateDependencies.FirstOrDefaultAsync(c => c.Id == certId);
        if (cert is null)
        {
            return (false, "Certificate dependency not found.");
        }

        var safeEvidence = SecretLeakGuard.Redact(evidence.Trim());
        var safeReplicas = SecretLeakGuard.Redact(replicasOrClientsUpdated.Trim());
        var rotatedUtc = DateTime.SpecifyKind(rotatedAtUtc, DateTimeKind.Utc);
        db.CertificateRenewalEvents.Add(new CertificateRenewalEvent
        {
            CertificateDependencyId = cert.Id,
            EventType = CertificateEventType.KeyRotation,
            OccurredAtUtc = rotatedUtc,
            PreviousExpiresAtUtc = cert.ExpiresAtUtc,
            NewExpiresAtUtc = cert.ExpiresAtUtc,
            Evidence = safeEvidence,
            ReplicasOrClientsUpdated = safeReplicas,
            RecordedAtUtc = DateTime.UtcNow,
            RecordedBy = actorName
        });

        cert.LastRotatedAtUtc = rotatedUtc;
        cert.UpdatedAtUtc = DateTime.UtcNow;
        cert.UpdatedBy = actorName;
        await db.SaveChangesAsync();

        var instance = await db.DatabaseInstances.FirstAsync(i => i.Id == cert.DatabaseInstanceId);
        await audit.RecordUserActionAsync("", actorName, "CertificateKeyRotationRecorded", "DatabaseInstance", cert.DatabaseInstanceId.ToString(), instance.Name,
            reason: $"{cert.Label} rotated {rotatedUtc:yyyy-MM-dd}" + (string.IsNullOrWhiteSpace(safeReplicas) ? "" : $"; updated: {safeReplicas}"));
        return (true, "");
    }

    /// <summary>One sweep: one deduplicated notice per certificate that crossed a more urgent expiry threshold, routed to the certificate's owner
    /// (owner email, then owner team). The instance contact gets it only when no owner address exists. Shared by the hosted service and the manual action.</summary>
    public async Task<int> EvaluateAndNotifyAsync(CancellationToken ct = default)
    {
        using var db = await factory.CreateDbContextAsync(ct);
        var thresholds = SupportRunwayNoticeEvaluator.ParseThresholds(configuration["CertificateExpiry:NoticeDaysThresholds"]);
        var now = DateTime.UtcNow;

        var certs = await db.CertificateDependencies.Where(c => c.ExpiresAtUtc != null).ToListAsync(ct);
        var notifiedCount = 0;

        foreach (var cert in certs)
        {
            var daysRemaining = (int)Math.Floor((cert.ExpiresAtUtc!.Value - now).TotalDays);
            var crossed = SupportRunwayNoticeEvaluator.ThresholdCrossed(daysRemaining, thresholds);
            if (crossed is null || !SupportRunwayNoticeEvaluator.ShouldNotify(daysRemaining, thresholds, cert.LastExpiryNoticeThresholdDays))
            {
                continue;
            }

            var instance = await db.DatabaseInstances.AsNoTracking().FirstOrDefaultAsync(i => i.Id == cert.DatabaseInstanceId, ct);
            if (instance is null)
            {
                continue;
            }

            var daysDescription = daysRemaining <= 0 ? $"{Math.Abs(daysRemaining)} day(s) overdue" : $"{daysRemaining} day(s) remaining";
            var owner = string.IsNullOrWhiteSpace(cert.OwnerName) ? "" : $" Owner: {cert.OwnerName}.";
            var message = $"{instance.Name}: certificate \"{cert.Label}\" ({cert.Kind}) expiry: {daysDescription}.{owner}";
            await ownerNotices.SendAsync(new OwnerNotice(
                $"DBTracker certificate-expiry notice: {instance.Name}",
                message,
                daysRemaining <= 0 ? NotificationSeverity.Critical : NotificationSeverity.Warning,
                cert.OwnerEmail,
                cert.OwnerTeamId,
                instance,
                instance.Name), ct);

            cert.LastExpiryNoticeThresholdDays = crossed;
            cert.LastExpiryNoticeAtUtc = now;
            notifiedCount++;
        }

        await db.SaveChangesAsync(ct);
        if (notifiedCount > 0)
        {
            await audit.RecordSystemActionAsync("CertificateExpiryCheck", "CertificateExpiryNoticesRaised", "DatabaseInstance", "", $"{notifiedCount} certificate(s)", ct: ct);
        }

        return notifiedCount;
    }
}
