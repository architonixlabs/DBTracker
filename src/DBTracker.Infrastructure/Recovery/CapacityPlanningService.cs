using DBTracker.Core.Entities;
using DBTracker.Core.Enums;
using DBTracker.Core.Services;
using DBTracker.Infrastructure.Audit;
using DBTracker.Infrastructure.Identity;
using Microsoft.EntityFrameworkCore;

namespace DBTracker.Infrastructure.Recovery;

public record WorkloadComparisonRow(WorkloadComparison Comparison, WorkloadComparisonResult Result);

/// <summary>A campaign the instance belongs to, for linking estimates and comparisons.</summary>
public record CapacityCampaignOption(int Id, string Name, CampaignState State);

public record CapacityPlanningView(
    DatabaseInstance Instance,
    IReadOnlyList<CapacityMeasurement> Measurements,
    IReadOnlyList<MigrationCapacityEstimate> Estimates,
    IReadOnlyList<WorkloadComparisonRow> Comparisons,
    CapacityForecastResult Forecast,
    InstanceCapacityLimit? SavedLimit,
    IReadOnlyList<CapacityCampaignOption> Campaigns);

/// <summary>Capacity measurements, the saved capacity limit, migration space estimates and pre/post workload comparisons. Projection math lives
/// in CapacityForecastBuilder and comparison math in WorkloadComparisonEvaluator; nothing here profiles or tunes anything.</summary>
public class CapacityPlanningService(IDbContextFactory<DBTrackerDbContext> factory, AuditRecorder audit)
{
    private static readonly string[] EditorRoles = [Roles.Admin, Roles.Dba];
    private static readonly TimeSpan FutureTolerance = TimeSpan.FromMinutes(5);

    /// <param name="capacityLimitGbOverride">A what-if limit for this view only; null uses the saved limit.</param>
    public async Task<CapacityPlanningView?> GetForInstanceAsync(int instanceId, double? capacityLimitGbOverride = null)
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
            .Include(e => e.MigrationCampaign)
            .Where(e => e.DatabaseInstanceId == instanceId)
            .OrderByDescending(e => e.EstimatedAtUtc)
            .ToListAsync();
        var comparisons = await db.WorkloadComparisons.AsNoTracking()
            .Include(c => c.PreMeasurement)
            .Include(c => c.PostMeasurement)
            .Include(c => c.MigrationCampaign)
            .Where(c => c.DatabaseInstanceId == instanceId)
            .OrderByDescending(c => c.RecordedAtUtc)
            .ToListAsync();
        var savedLimit = await db.InstanceCapacityLimits.AsNoTracking().FirstOrDefaultAsync(l => l.DatabaseInstanceId == instanceId);
        var campaigns = await db.CampaignInstanceLinks.AsNoTracking()
            .Where(l => l.DatabaseInstanceId == instanceId)
            .Select(l => new CapacityCampaignOption(l.MigrationCampaign!.Id, l.MigrationCampaign.Name, l.MigrationCampaign.State))
            .ToListAsync();

        var now = DateTime.UtcNow;
        var forecast = CapacityForecastBuilder.Build(measurements, capacityLimitGbOverride ?? savedLimit?.CapacityLimitGb, now);
        var comparisonRows = comparisons.Select(c => new WorkloadComparisonRow(c, WorkloadComparisonEvaluator.Compare(c.PreMeasurement, c.PostMeasurement, now))).ToList();

        return new CapacityPlanningView(instance, measurements, estimates, comparisonRows, forecast, savedLimit, campaigns);
    }

    public async Task<(bool Ok, string Error)> AddMeasurementAsync(CapacityMeasurement measurement, string actorName)
    {
        if (measurement.MeasuredAtUtc > DateTime.UtcNow + FutureTolerance)
        {
            return (false, "The measurement time can't be in the future.");
        }
        if (string.IsNullOrWhiteSpace(measurement.Unit))
        {
            return (false, "Give the unit, e.g. GB or ms.");
        }

        using var db = await factory.CreateDbContextAsync();
        measurement.MeasuredAtUtc = DateTime.SpecifyKind(measurement.MeasuredAtUtc, DateTimeKind.Utc);
        measurement.Notes = SecretLeakGuard.Redact(measurement.Notes);
        measurement.CreatedAtUtc = DateTime.UtcNow;
        measurement.RecordedBy = actorName;
        db.CapacityMeasurements.Add(measurement);
        await db.SaveChangesAsync();

        var instance = await db.DatabaseInstances.FirstAsync(i => i.Id == measurement.DatabaseInstanceId);
        await audit.RecordUserActionAsync("", actorName, "CapacityMeasurementRecorded", "DatabaseInstance", measurement.DatabaseInstanceId.ToString(), instance.Name,
            reason: $"{measurement.MeasurementType}: {measurement.Value} {measurement.Unit} at {measurement.MeasuredAtUtc:u}");
        return (true, "");
    }

    /// <summary>Saves the storage limit the forecast compares against, or clears it with null.</summary>
    public async Task<(bool Ok, string Error)> SetCapacityLimitAsync(int instanceId, double? limitGb, string source, string actorName, IReadOnlyCollection<string> actorRoles)
    {
        if (!actorRoles.Any(EditorRoles.Contains))
        {
            return (false, "Only Admin and DBA users can change the capacity limit.");
        }
        if (limitGb is <= 0)
        {
            return (false, "The capacity limit must be more than 0 GB.");
        }
        if (source.Trim().Length > 300)
        {
            return (false, "Keep the source under 300 characters.");
        }

        using var db = await factory.CreateDbContextAsync();
        var instance = await db.DatabaseInstances.FirstOrDefaultAsync(i => i.Id == instanceId);
        if (instance is null)
        {
            return (false, "Instance not found.");
        }

        var existing = await db.InstanceCapacityLimits.FirstOrDefaultAsync(l => l.DatabaseInstanceId == instanceId);
        var previous = existing?.CapacityLimitGb;
        if (limitGb is null)
        {
            if (existing is not null)
            {
                db.InstanceCapacityLimits.Remove(existing);
            }
        }
        else
        {
            if (existing is null)
            {
                existing = new InstanceCapacityLimit { DatabaseInstanceId = instanceId };
                db.InstanceCapacityLimits.Add(existing);
            }
            existing.CapacityLimitGb = limitGb.Value;
            existing.Source = source.Trim();
            existing.UpdatedAtUtc = DateTime.UtcNow;
            existing.UpdatedBy = actorName;
        }
        await db.SaveChangesAsync();

        await audit.RecordUserActionAsync("", actorName, limitGb is null ? "CapacityLimitCleared" : "CapacityLimitSet", "DatabaseInstance", instanceId.ToString(), instance.Name,
            reason: $"{previous?.ToString("N0") ?? "none"} GB -> {limitGb?.ToString("N0") ?? "none"} GB{(string.IsNullOrWhiteSpace(source) ? "" : $" ({source.Trim()})")}");
        return (true, "");
    }

    public async Task<(bool Ok, string Error)> AddEstimateAsync(MigrationCapacityEstimate estimate, string actorName)
    {
        if (string.IsNullOrWhiteSpace(estimate.AssumptionsText))
        {
            return (false, "A migration space estimate must state its assumptions.");
        }

        using var db = await factory.CreateDbContextAsync();
        if (estimate.MigrationCampaignId is { } campaignId &&
            !await db.CampaignInstanceLinks.AnyAsync(l => l.MigrationCampaignId == campaignId && l.DatabaseInstanceId == estimate.DatabaseInstanceId))
        {
            return (false, "That campaign doesn't include this instance.");
        }

        estimate.EstimatedAtUtc = DateTime.UtcNow;
        estimate.EstimatedBy = actorName;
        db.MigrationCapacityEstimates.Add(estimate);
        await db.SaveChangesAsync();

        var instance = await db.DatabaseInstances.FirstAsync(i => i.Id == estimate.DatabaseInstanceId);
        await audit.RecordUserActionAsync("", actorName, "MigrationCapacityEstimateRecorded", "DatabaseInstance", estimate.DatabaseInstanceId.ToString(), instance.Name,
            reason: (estimate.EstimatedSpaceGb is { } gb ? $"{gb:N0} GB estimated" : "estimate recorded") +
                    (estimate.MigrationCampaignId is { } linked ? $" for campaign #{linked}" : ""));
        return (true, "");
    }

    /// <summary>Records a pre/post comparison between two of this instance's measurements, optionally for one of its campaigns.</summary>
    public async Task<(bool Ok, string Error)> AddComparisonAsync(WorkloadComparison comparison, string actorName, IReadOnlyCollection<string> actorRoles)
    {
        if (!actorRoles.Any(EditorRoles.Contains))
        {
            return (false, "Only Admin and DBA users can record comparisons.");
        }

        using var db = await factory.CreateDbContextAsync();
        var pre = comparison.PreMeasurementId is { } preId ? await db.CapacityMeasurements.AsNoTracking().FirstOrDefaultAsync(m => m.Id == preId) : null;
        var post = comparison.PostMeasurementId is { } postId ? await db.CapacityMeasurements.AsNoTracking().FirstOrDefaultAsync(m => m.Id == postId) : null;
        var errors = WorkloadComparisonEvaluator.ValidateForSave(pre, post, comparison.DatabaseInstanceId);
        if (errors.Count > 0)
        {
            return (false, string.Join(" ", errors));
        }
        if (comparison.MigrationCampaignId is { } campaignId &&
            !await db.CampaignInstanceLinks.AnyAsync(l => l.MigrationCampaignId == campaignId && l.DatabaseInstanceId == comparison.DatabaseInstanceId))
        {
            return (false, "That campaign doesn't include this instance.");
        }

        comparison.ComparisonNotes = SecretLeakGuard.Redact(comparison.ComparisonNotes.Trim());
        comparison.RecordedAtUtc = DateTime.UtcNow;
        comparison.RecordedBy = actorName;
        db.WorkloadComparisons.Add(comparison);
        await db.SaveChangesAsync();

        var result = WorkloadComparisonEvaluator.Compare(pre, post, DateTime.UtcNow);
        var instance = await db.DatabaseInstances.FirstAsync(i => i.Id == comparison.DatabaseInstanceId);
        await audit.RecordUserActionAsync("", actorName, "WorkloadComparisonRecorded", "DatabaseInstance", comparison.DatabaseInstanceId.ToString(), instance.Name,
            reason: result.Summary + (comparison.MigrationCampaignId is { } linked ? $" Campaign #{linked}." : ""));
        return (true, "");
    }

    /// <summary>Parses an import file against the current inventory and recorded measurements. Nothing is written.</summary>
    public async Task<EvidenceImportPreview<CapacityMeasurement>> PreviewImportAsync(string csvContent)
    {
        using var db = await factory.CreateDbContextAsync();
        var (idsByName, blocked) = await EvidenceImportLookup.LoadInstancesAsync(db);
        var ids = idsByName.Values.ToList();
        var existing = await db.CapacityMeasurements.AsNoTracking().Where(m => ids.Contains(m.DatabaseInstanceId)).ToListAsync();
        return CapacityMeasurementCsvImporter.Parse(csvContent, idsByName, blocked, existing, DateTime.UtcNow);
    }

    /// <summary>Re-parses the file at apply time, so anything recorded since the preview is skipped, and records only its Create rows in one save.</summary>
    public async Task<(bool Ok, string Message, int Created)> ApplyImportAsync(string csvContent, string fileName, string actorUserId, string actorName,
        IReadOnlyCollection<string> actorRoles)
    {
        if (!actorRoles.Any(EditorRoles.Contains))
        {
            return (false, "Only Admin and DBA users can import measurements.", 0);
        }

        var preview = await PreviewImportAsync(csvContent);
        var creates = preview.Rows.Where(r => r.Plan == EvidenceImportRowPlan.Create && r.Record is not null).Select(r => r.Record!).ToList();
        if (creates.Count == 0)
        {
            return (false, "Nothing new to import.", 0);
        }

        using var db = await factory.CreateDbContextAsync();
        var now = DateTime.UtcNow;
        foreach (var m in creates)
        {
            m.CreatedAtUtc = now;
            m.RecordedBy = actorName;
        }
        db.CapacityMeasurements.AddRange(creates);
        await db.SaveChangesAsync();

        await audit.RecordUserActionAsync(actorUserId, actorName, "CapacityMeasurementsCsvImported", "CsvImport", "", fileName,
            reason: $"{creates.Count} recorded, {preview.SkipCount} already on record, {preview.ConflictCount} conflicting, {preview.Errors.Count} error row(s)");
        return (true, $"{creates.Count} measurement(s) recorded.", creates.Count);
    }
}
