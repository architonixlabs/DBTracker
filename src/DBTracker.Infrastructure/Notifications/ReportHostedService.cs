using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace DBTracker.Infrastructure.Notifications;

/// <summary>
/// Periodically emails the compliance report to "Reports:DistributionList". Interval in days via
/// "Reports:IntervalDays" (default 7 — weekly); &lt;= 0 disables the schedule. "Send now" on the
/// Compliance page works regardless of whether the schedule is enabled.
/// </summary>
public class ReportHostedService(
    IServiceScopeFactory scopeFactory,
    IConfiguration configuration,
    ILogger<ReportHostedService> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var intervalDays = configuration.GetValue("Reports:IntervalDays", 7);
        if (intervalDays <= 0)
        {
            logger.LogInformation("Scheduled compliance report disabled (Reports:IntervalDays <= 0).");
            return;
        }

        var interval = TimeSpan.FromDays(intervalDays);

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await Task.Delay(interval, stoppingToken);
            }
            catch (OperationCanceledException)
            {
                break;
            }

            try
            {
                using var scope = scopeFactory.CreateScope();
                var reportService = scope.ServiceProvider.GetRequiredService<ComplianceReportService>();
                var result = await reportService.SendNowAsync(stoppingToken);
                logger.LogInformation(
                    "Scheduled compliance report: {Accepted} accepted, {Failed} failed, {Total} recipient(s) total.",
                    result.AcceptedCount, result.FailedCount, result.Outcomes.Count);
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Scheduled compliance report failed to send.");
            }
        }
    }
}
