using DBTracker.Infrastructure.Diagnostics;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace DBTracker.Infrastructure.Recovery;

/// <summary>
/// Periodic sweep for D20's certificate-expiry notices — same shape as SupportRunwayCheckHostedService.
/// "CertificateExpiry:IntervalMinutes" (default 1440, once a day); &lt;= 0 disables it. The same logic
/// is also reachable synchronously via an explicit "Evaluate now" admin action for testing.
/// </summary>
public class CertificateExpiryCheckHostedService(
    IServiceScopeFactory scopeFactory,
    IConfiguration configuration,
    WorkerHeartbeatStore heartbeats,
    ILogger<CertificateExpiryCheckHostedService> logger) : BackgroundService
{
    public const string WorkerName = "Certificate expiry check";

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var intervalMinutes = configuration.GetValue("CertificateExpiry:IntervalMinutes", 1440);
        if (intervalMinutes <= 0)
        {
            logger.LogInformation("Certificate expiry check background service disabled (CertificateExpiry:IntervalMinutes <= 0).");
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
                var service = scope.ServiceProvider.GetRequiredService<CertificateDependencyService>();
                await service.EvaluateAndNotifyAsync(stoppingToken);
                heartbeats.RanSuccessfully(WorkerName, interval);
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Certificate expiry check sweep failed.");
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
