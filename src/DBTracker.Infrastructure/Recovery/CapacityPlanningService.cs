using DBTracker.Core.Entities;
using DBTracker.Core.Enums;
using DBTracker.Core.Services;
using DBTracker.Infrastructure.Audit;
using Microsoft.EntityFrameworkCore;

namespace DBTracker.Infrastructure.Recovery;

public record CapacityPlanningView(
    DatabaseInstance Instance,
    IReadOnlyList<CapacityMeasurement> Measurements,
    IReadOnlyList<MigrationCapacityEstimate> Estimates,
    IReadOnlyList<WorkloadComparison> Comparisons,
    CapacityForecastResult Forecast);

/// <summary>Capacity measurements, migration space estimates and workload comparisons. Projection math lives in CapacityForecastBuilder.</summary>
public class CapacityPlanningService(IDbContextFactory<DBTrackerDbContext> factory, AuditRecorder audit)
{
    public async Task<CapacityPlanningView?> GetForInstanceAsync(int instanceId, double? capacityLimitGb)
    {
        using var db = await factory.CreateDbContextAsync();
        var instance = await db.DatabaseInstances.AsNoTracking().FirstOrDefaultAsync(i => i.Id == instanceId);
        if (instance is null)
        {
            return null;
        }

        var measurements = await db.CapacityMeasurements.AsNoTracking()
            .Where(m => m.DatabaseInstanceId == instanceId)
            .OrderByDescending(m => m.MeasuredAtUtc)
            .ToListAsync();
        var estimates = await db.MigrationCapacityEstimates.AsNoTracking()
            .Where(e => e.DatabaseInstanceId == instanceId)
            .OrderByDescending(e => e.EstimatedAtUtc)
            .ToListAsync();
        var comparisons = await db.WorkloadComparisons.AsNoTracking()
            .Include(c => c.PreMeasurement)
            .Include(c => c.PostMeasurement)
            .Where(c => c.DatabaseInstanceId == instanceId)
            .OrderByDescending(c => c.RecordedAtUtc)
            .ToListAsync();

        var forecast = CapacityForecastBuilder.Build(measurements, capacityLimitGb, DateTime.UtcNow);

        return new CapacityPlanningView(instance, measurements, estimates, comparisons, forecast);
    }

    public async Task<CapacityMeasurement> AddMeasurementAsync(CapacityMeasurement measurement, string actorName)
    {
        using var db = await factory.CreateDbContextAsync();
        measurement.CreatedAtUtc = DateTime.UtcNow;
        measurement.RecordedBy = actorName;
        db.CapacityMeasurements.Add(measurement);
        await db.SaveChangesAsync();

        var instance = await db.DatabaseInstances.FirstAsync(i => i.Id == measurement.DatabaseInstanceId);
        await audit.RecordUserActionAsync("", actorName, "CapacityMeasurementRecorded", "DatabaseInstance", measurement.DatabaseInstanceId.ToString(), instance.Name,
            reason: $"{measurement.MeasurementType}: {measurement.Value} {measurement.Unit}");
        return measurement;
    }

    public async Task<(bool Ok, string Error)> AddEstimateAsync(MigrationCapacityEstimate estimate, string actorName)
    {
        if (string.IsNullOrWhiteSpace(estimate.AssumptionsText))
        {
            return (false, "A migration space estimate must state its assumptions.");
        }

        using var db = await factory.CreateDbContextAsync();
        estimate.EstimatedAtUtc = DateTime.UtcNow;
        estimate.EstimatedBy = actorName;
        db.MigrationCapacityEstimates.Add(estimate);
        await db.SaveChangesAsync();

        var instance = await db.DatabaseInstances.FirstAsync(i => i.Id == estimate.DatabaseInstanceId);
        await audit.RecordUserActionAsync("", actorName, "MigrationCapacityEstimateRecorded", "DatabaseInstance", estimate.DatabaseInstanceId.ToString(), instance.Name,
            reason: estimate.EstimatedSpaceGb is { } gb ? $"{gb:N0} GB estimated" : "estimate recorded");
        return (true, "");
    }

    public async Task<WorkloadComparison> AddComparisonAsync(WorkloadComparison comparison, string actorName)
    {
        using var db = await factory.CreateDbContextAsync();
        comparison.RecordedAtUtc = DateTime.UtcNow;
        comparison.RecordedBy = actorName;
        db.WorkloadComparisons.Add(comparison);
        await db.SaveChangesAsync();

        var instance = await db.DatabaseInstances.FirstAsync(i => i.Id == comparison.DatabaseInstanceId);
        await audit.RecordUserActionAsync("", actorName, "WorkloadComparisonRecorded", "DatabaseInstance", comparison.DatabaseInstanceId.ToString(), instance.Name);
        return comparison;
    }
}
