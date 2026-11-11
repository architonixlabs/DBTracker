using DBTracker.Infrastructure.Diagnostics;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace DBTracker.Infrastructure.Notifications;

/// <summary>Emails the compliance report to Reports:DistributionList every Reports:IntervalDays (default 7); &lt;= 0 disables the schedule. Send now works either way.</summary>
public class ReportHostedService(
    IServiceScopeFactory scopeFactory,
    Install.InstallState installState,
    IConfiguration configuration,
    WorkerHeartbeatStore heartbeats,
    ILogger<ReportHostedService> logger) : BackgroundService
{
    public const string WorkerName = "Scheduled report";

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var intervalDays = configuration.GetValue("Reports:IntervalDays", 7);
        if (intervalDays <= 0)
        {
            logger.LogInformation("Scheduled compliance report disabled (Reports:IntervalDays <= 0).");
            heartbeats.Disabled(WorkerName);
            return;
        }

        var interval = TimeSpan.FromDays(intervalDays);
        await installState.WaitUntilInstalledAsync(stoppingToken);
        heartbeats.Starting(WorkerName, interval);

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
                heartbeats.RanSuccessfully(WorkerName, interval);
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Scheduled compliance report failed to send.");
                heartbeats.RanWithError(WorkerName, interval, ex.Message);
            }
        }
    }
}
