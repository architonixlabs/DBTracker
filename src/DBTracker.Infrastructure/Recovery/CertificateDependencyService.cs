using DBTracker.Core.Entities;
using DBTracker.Core.Enums;
using DBTracker.Core.Services;
using DBTracker.Infrastructure.Audit;
using DBTracker.Infrastructure.Identity;
using DBTracker.Infrastructure.Notifications;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;

namespace DBTracker.Infrastructure.Recovery;

public record CertificateRow(CertificateDependency Certificate, CertificateExpiryStatus ExpiryStatus, KeyRotationStatus RotationStatus);

/// <summary>Certificate and recovery-key reminders. Free-text fields go through SecretLeakGuard before saving, so a pasted secret never reaches the database.</summary>
public class CertificateDependencyService(
    IDbContextFactory<DBTrackerDbContext> factory,
    AuditRecorder audit,
    NotificationService notifications,
    NotificationSubscriptionService subscriptionService,
    IEmailSender emailSender,
    UserManager<ApplicationUser> userManager,
    IConfiguration configuration)
{
    public async Task<List<CertificateRow>> ListForInstanceAsync(int instanceId)
    {
        using var db = await factory.CreateDbContextAsync();
        var certs = await db.CertificateDependencies.AsNoTracking()
            .Include(c => c.OwnerTeam)
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

    public async Task RecordRotationAsync(int certId, DateTime rotatedAtUtc, string evidence, string actorName)
    {
        using var db = await factory.CreateDbContextAsync();
        var cert = await db.CertificateDependencies.FirstOrDefaultAsync(c => c.Id == certId);
        if (cert is null)
        {
            return;
        }

        cert.LastRotatedAtUtc = rotatedAtUtc;
        cert.RenewalEvidenceText = SecretLeakGuard.Redact(evidence);
        cert.UpdatedAtUtc = DateTime.UtcNow;
        cert.UpdatedBy = actorName;
        await db.SaveChangesAsync();

        var instance = await db.DatabaseInstances.FirstAsync(i => i.Id == cert.DatabaseInstanceId);
        await audit.RecordUserActionAsync("", actorName, "CertificateKeyRotationRecorded", "DatabaseInstance", cert.DatabaseInstanceId.ToString(), instance.Name,
            reason: cert.Label);
    }

    /// <summary>One sweep: one deduplicated notice per certificate that crossed a more urgent expiry threshold, to the contact and subscribers. Shared by the hosted service and the manual action.</summary>
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

            var instance = await db.DatabaseInstances.FirstOrDefaultAsync(i => i.Id == cert.DatabaseInstanceId, ct);
            if (instance is null)
            {
                continue;
            }

            var daysDescription = daysRemaining <= 0 ? $"{Math.Abs(daysRemaining)} day(s) overdue" : $"{daysRemaining} day(s) remaining";
            var message = $"{instance.Name}: certificate \"{cert.Label}\" ({cert.Kind}) expiry: {daysDescription}.";
            await notifications.CreateAsync(instance, message, daysRemaining <= 0 ? NotificationSeverity.Critical : NotificationSeverity.Warning);

            var recipientEmails = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            if (!string.IsNullOrWhiteSpace(instance.ContactEmail))
            {
                recipientEmails.Add(instance.ContactEmail);
            }
            foreach (var userId in await subscriptionService.GetEmailSubscriberUserIdsAsync(instance.Id, instance.Company, instance.OwnerTeamId))
            {
                var user = await userManager.FindByIdAsync(userId);
                if (user?.Email is { Length: > 0 } subscriberEmail)
                {
                    recipientEmails.Add(subscriberEmail);
                }
            }
            foreach (var email in recipientEmails)
            {
                await emailSender.SendAsync(email, $"DBTracker certificate-expiry notice: {instance.Name}", message, ct);
            }

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
