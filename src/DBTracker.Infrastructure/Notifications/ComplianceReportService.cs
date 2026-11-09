using DBTracker.Core.Services;
using DBTracker.Infrastructure.Queries;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace DBTracker.Infrastructure.Notifications;

public class ComplianceReportService(
    InstanceOverviewService overviewService,
    IEmailSender emailSender,
    IConfiguration configuration,
    ILogger<ComplianceReportService> logger)
{
    /// <summary>Builds the current report and emails it to the configured distribution list. Used by both the schedule and "Send now".</summary>
    public async Task<int> SendNowAsync(CancellationToken ct = default)
    {
        var recipients = (configuration["Reports:DistributionList"] ?? string.Empty)
            .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

        if (recipients.Length == 0)
        {
            logger.LogWarning("Compliance report requested but Reports:DistributionList is not configured.");
            return 0;
        }

        var overviews = await overviewService.GetAllAsync();
        var now = DateTime.UtcNow;

        var rows = overviews
            .Select(o => new ComplianceReportRow(
                o.Instance,
                o.Health,
                o.LastBackupUtc,
                o.Instance.RestoreEvents.Any(r => r.Outcome == Core.Enums.RestoreOutcome.Success)))
            .ToList();

        var subject = ComplianceReportBuilder.BuildSubject(rows, now);
        var body = ComplianceReportBuilder.BuildPlainTextBody(rows, now);

        foreach (var recipient in recipients)
        {
            await emailSender.SendAsync(recipient, subject, body, ct);
        }

        return recipients.Length;
    }
}
