using DBTracker.Infrastructure.Diagnostics;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace DBTracker.Infrastructure.Lifecycle;

/// <summary>Periodic support-runway notice sweep. SupportRunway:IntervalMinutes (default 1440); &lt;= 0 disables it.</summary>
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
