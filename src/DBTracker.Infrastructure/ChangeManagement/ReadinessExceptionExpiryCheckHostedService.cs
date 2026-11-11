using DBTracker.Infrastructure.Diagnostics;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace DBTracker.Infrastructure.ChangeManagement;

/// <summary>Periodic sweep for exception-expiry notices. ReadinessException:IntervalMinutes (default 1440); &lt;= 0 disables it.</summary>
public class ReadinessExceptionExpiryCheckHostedService(
    IServiceScopeFactory scopeFactory,
    Install.InstallState installState,
    IConfiguration configuration,
    WorkerHeartbeatStore heartbeats,
    ILogger<ReadinessExceptionExpiryCheckHostedService> logger) : BackgroundService
{
    public const string WorkerName = "Readiness exception expiry check";

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var intervalMinutes = configuration.GetValue("ReadinessException:IntervalMinutes", 1440);
        if (intervalMinutes <= 0)
        {
            logger.LogInformation("Readiness exception expiry check disabled (ReadinessException:IntervalMinutes <= 0).");
            heartbeats.Disabled(WorkerName);
            return;
        }

        var interval = TimeSpan.FromMinutes(intervalMinutes);
        await installState.WaitUntilInstalledAsync(stoppingToken);
        heartbeats.Starting(WorkerName, interval);

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                using var scope = scopeFactory.CreateScope();
                var service = scope.ServiceProvider.GetRequiredService<ReadinessExceptionService>();
                await service.EvaluateAndNotifyExpiringAsync(stoppingToken);
                heartbeats.RanSuccessfully(WorkerName, interval);
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Readiness exception expiry sweep failed.");
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
