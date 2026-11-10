using DBTracker.Infrastructure.Diagnostics;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace DBTracker.Infrastructure.Lifecycle;

/// <summary>
/// Periodic sweep for D03's support-runway notices — same shape as AlertCheckHostedService.
/// "SupportRunway:IntervalMinutes" (default 1440, once a day — a support deadline doesn't need
/// 10-minute polling); &lt;= 0 disables it. The same logic is also reachable synchronously via an
/// explicit "Evaluate now" admin action (SupportRunwayService.EvaluateAndNotifyAsync) for testing
/// and for an administrator who doesn't want to wait for the next sweep.
/// </summary>
public class SupportRunwayCheckHostedService(
    IServiceScopeFactory scopeFactory,
    IConfiguration configuration,
    WorkerHeartbeatStore heartbeats,
    ILogger<SupportRunwayCheckHostedService> logger) : BackgroundService
{
    public const string WorkerName = "Support runway check";

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var intervalMinutes = configuration.GetValue("SupportRunway:IntervalMinutes", 1440);
        if (intervalMinutes <= 0)
        {
            logger.LogInformation("Support runway check background service disabled (SupportRunway:IntervalMinutes <= 0).");
            heartbeats.Disabled(WorkerName);
            return;
        }

        var interval = TimeSpan.FromMinutes(intervalMinutes);
        heartbeats.Starting(WorkerName, interval);

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                using var scope = scopeFactory.CreateScope();
                var service = scope.ServiceProvider.GetRequiredService<SupportRunwayService>();
                await service.EvaluateAndNotifyAsync(stoppingToken);
                heartbeats.RanSuccessfully(WorkerName, interval);
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Support runway check sweep failed.");
                heartbeats.RanWithError(WorkerName, interval, ex.Message);
            }

            try
            {
                await Task.Delay(interval, stoppingToken);
            }
            catch (OperationCanceledException)
            {
                // shutting down
            }
        }
    }
}
