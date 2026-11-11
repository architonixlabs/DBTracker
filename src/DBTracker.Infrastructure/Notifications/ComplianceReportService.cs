using DBTracker.Core.Services;
using DBTracker.Infrastructure.Queries;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace DBTracker.Infrastructure.Notifications;

/// <summary>Per-recipient send outcome; a disabled sender is never reported as sent.</summary>
public record ReportRecipientOutcome(string Recipient, EmailSendResult Result);

/// <summary>What sending would do, computed from the same rows that get sent and shown before confirmation.</summary>
public record ReportPreview(ReportScope Scope, DateTime GenerationTimeUtc, string Subject, string Body, IReadOnlyList<string> Recipients, int RowCount);

/// <summary>Result of a send: generation time, scope, body and per-recipient outcomes. No recipients and a Disabled sender both mean nothing was sent.</summary>
public record ReportSendResult(ReportScope Scope, DateTime GenerationTimeUtc, IReadOnlyList<ReportRecipientOutcome> Outcomes)
{
    public int AcceptedCount => Outcomes.Count(o => o.Result.Outcome == Core.Enums.EmailSendOutcome.Accepted);
    public int FailedCount => Outcomes.Count(o => o.Result.Outcome == Core.Enums.EmailSendOutcome.Failed);
    public int DisabledCount => Outcomes.Count(o => o.Result.Outcome == Core.Enums.EmailSendOutcome.Disabled);
    public bool Disabled => Outcomes.Count == 0 || DisabledCount == Outcomes.Count;
}

public class ComplianceReportService(
    InstanceOverviewService overviewService,
    IEmailSender emailSender,
    IConfiguration configuration,
    DeliveryAttemptService deliveryAttempts,
    ILogger<ComplianceReportService> logger)
{
    /// <summary>Builds the report for a scope without sending; the same dataset Send uses, so preview, download and send can't drift.</summary>
    public async Task<ReportPreview> BuildPreviewAsync(ReportScope scope, DateTime? generationTimeUtc = null)
    {
        var now = generationTimeUtc ?? DateTime.UtcNow;
        var rows = await BuildRowsAsync(scope);
        var recipients = ParseRecipients();

        return new ReportPreview(
            scope, now,
            ComplianceReportBuilder.BuildSubject(rows, now),
            ComplianceReportBuilder.BuildPlainTextBody(rows, now),
            recipients, rows.Count);
    }

    /// <summary>Builds and sends the report for the confirmed scope, with a real outcome per recipient, and stores the attempt for /setup.</summary>
    /// <param name="triggeredBy">Who asked for it: a user name, or "Background: Scheduled report".</param>
    public async Task<ReportSendResult> SendAsync(ReportScope scope, string triggeredBy, CancellationToken ct = default)
    {
        var now = DateTime.UtcNow;
        var recipients = ParseRecipients();

        if (recipients.Count == 0)
        {
            logger.LogWarning("Compliance report requested but Reports:DistributionList is not configured.");
            await deliveryAttempts.RecordAsync(Core.Enums.DeliveryKind.ComplianceReport, scope.Description, [], triggeredBy, attemptedAtUtc: now, ct: ct);
            return new ReportSendResult(scope, now, []);
        }

        var rows = await BuildRowsAsync(scope);
        var subject = ComplianceReportBuilder.BuildSubject(rows, now);
        var body = ComplianceReportBuilder.BuildPlainTextBody(rows, now);

        var outcomes = new List<ReportRecipientOutcome>(recipients.Count);
        foreach (var recipient in recipients)
        {
            var result = await emailSender.SendAsync(recipient, subject, body, ct);
            outcomes.Add(new ReportRecipientOutcome(recipient, result));
            if (result.Outcome == Core.Enums.EmailSendOutcome.Failed)
            {
                logger.LogWarning("Compliance report to {Recipient} failed: {Detail}", recipient, result.Detail);
            }
        }

        await deliveryAttempts.RecordAsync(Core.Enums.DeliveryKind.ComplianceReport,
            $"{scope.Description} ({rows.Count} instance(s))", outcomes.Select(o => o.Result).ToList(), triggeredBy, attemptedAtUtc: now, ct: ct);

        return new ReportSendResult(scope, now, outcomes);
    }

    /// <summary>Used by the scheduled job, which always sends the full inventory.</summary>
    public async Task<ReportSendResult> SendNowAsync(CancellationToken ct = default) =>
        await SendAsync(ReportScope.Full(), AuditEventFactory.SystemActorPrefix + ReportHostedService.WorkerName, ct);

    private async Task<List<ComplianceReportRow>> BuildRowsAsync(ReportScope scope)
    {
        var overviews = await overviewService.GetAllLatestAsync();

        return overviews
            .Where(o => scope.Includes(o.Instance.Id))
            .Select(o => new ComplianceReportRow(
                o.Instance,
                o.Health,
                o.LastBackupUtc,
                RestoreEvidence.AnyVerified(o.Instance.RestoreEvents),
                BackupEvidenceClassifier.ClassifyLatestSuccess(o.Instance.BackupRecords.ToList(), o.Instance.RestoreEvents.ToList(), DateTime.UtcNow)))
            .ToList();
    }

    private List<string> ParseRecipients() =>
        (configuration["Reports:DistributionList"] ?? string.Empty)
            .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .ToList();
}
