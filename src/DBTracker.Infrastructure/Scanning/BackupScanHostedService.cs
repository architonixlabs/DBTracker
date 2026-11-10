using DBTracker.Infrastructure.Diagnostics;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace DBTracker.Infrastructure.Scanning;

/// <summary>
/// Periodically re-scans every instance that has a backup file location configured, so discovered
/// backup evidence stays current without anyone manually re-triggering it. Interval is configurable
/// via "BackupScan:IntervalMinutes" (default 15); a value &lt;= 0 disables the background scan
/// entirely (manual "Scan now" from the instance page still works).
/// </summary>
public class BackupScanHostedService(
    IServiceScopeFactory scopeFactory,
    IConfiguration configuration,
    WorkerHeartbeatStore heartbeats,
    ILogger<BackupScanHostedService> logger) : BackgroundService
{
    public const string WorkerName = "Backup scan";

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var intervalMinutes = configuration.GetValue("BackupScan:IntervalMinutes", 15);
        if (intervalMinutes <= 0)
        {
            logger.LogInformation("Backup scan background service disabled (BackupScan:IntervalMinutes <= 0).");
            heartbeats.Disabled(WorkerName);
            return;
        }

        var interval = TimeSpan.FromMinutes(intervalMinutes);
        heartbeats.Starting(WorkerName, interval);

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await ScanAllConfiguredInstancesAsync(stoppingToken);
                heartbeats.RanSuccessfully(WorkerName, interval);
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Backup scan sweep failed.");
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

    private async Task ScanAllConfiguredInstancesAsync(CancellationToken ct)
    {
        using var scope = scopeFactory.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<DBTrackerDbContext>();
        var scanner = scope.ServiceProvider.GetRequiredService<BackupFileScanner>();

        var instanceIds = await db.DatabaseInstances
            .Where(i => !i.IsArchived && i.BackupFileLocation != "")
            .Select(i => i.Id)
            .ToListAsync(ct);

        foreach (var id in instanceIds)
        {
            var result = await scanner.ScanAsync(id, ct);
            if (result.Outcome == ScanOutcome.Error)
            {
                logger.LogWarning("Backup scan failed for instance {InstanceId}: {Error}", id, result.ErrorMessage);
            }
        }
    }
}
