using DBTracker.Core.Services;
using DBTracker.Infrastructure.Queries;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace DBTracker.Infrastructure.Notifications;

/// <summary>Per-recipient send outcome (U09) — a no-op/disabled sender can never be reported as "sent".</summary>
public record ReportRecipientOutcome(string Recipient, EmailSendResult Result);

/// <summary>
/// What "Send report"/"Send now" would do, computed from the exact same row set that gets sent —
/// shown to the user before they confirm (U09).
/// </summary>
public record ReportPreview(ReportScope Scope, DateTime GenerationTimeUtc, string Subject, string Body, IReadOnlyList<string> Recipients, int RowCount);

/// <summary>
/// The result of an actual send: generation time/scope/body are recorded alongside per-recipient
/// outcomes. "No recipients configured" and "a configured recipient's send was Disabled" both mean
/// nothing was actually sent (U09) — Disabled covers both, so neither can be reported as accepted.
/// </summary>
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
    ILogger<ComplianceReportService> logger)
{
    /// <summary>
    /// Builds the report for the given scope without sending anything — the exact dataset "Send"
    /// will use, so preview/download/send can never drift from each other (U09).
    /// </summary>
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

    /// <summary>Builds the report for the confirmed scope and sends it, reporting a real per-recipient outcome for each address. Used by "Send now"/"Send report" and the schedule.</summary>
    public async Task<ReportSendResult> SendAsync(ReportScope scope, CancellationToken ct = default)
    {
        var now = DateTime.UtcNow;
        var recipients = ParseRecipients();

        if (recipients.Count == 0)
        {
            logger.LogWarning("Compliance report requested but Reports:DistributionList is not configured.");
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
        }

        return new ReportSendResult(scope, now, outcomes);
    }

    /// <summary>Preserved for the scheduled background job, which always sends the full inventory.</summary>
    public async Task<ReportSendResult> SendNowAsync(CancellationToken ct = default) => await SendAsync(ReportScope.Full(), ct);

    private async Task<List<ComplianceReportRow>> BuildRowsAsync(ReportScope scope)
    {
        var overviews = await overviewService.GetAllAsync();

        return overviews
            .Where(o => scope.Includes(o.Instance.Id))
            .Select(o => new ComplianceReportRow(
                o.Instance,
                o.Health,
                o.LastBackupUtc,
                o.Instance.RestoreEvents.Any(r => r.Outcome == Core.Enums.RestoreOutcome.Success)))
            .ToList();
    }

    private List<string> ParseRecipients() =>
        (configuration["Reports:DistributionList"] ?? string.Empty)
            .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .ToList();
}
