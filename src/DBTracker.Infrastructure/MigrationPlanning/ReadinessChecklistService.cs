using DBTracker.Core.Entities;
using DBTracker.Core.Enums;
using DBTracker.Infrastructure.Audit;
using Microsoft.EntityFrameworkCore;

namespace DBTracker.Infrastructure.MigrationPlanning;

/// <summary>Readiness checklists seeded at Unknown. UpdateTargetAsync invalidates every recorded item while keeping its original result; evidence expiry is computed on read, so it needs no write.</summary>
public class ReadinessChecklistService(IDbContextFactory<DBTrackerDbContext> factory, AuditRecorder audit)
{
    private static readonly (ReadinessCheckArea Area, string Label)[] StandardAreas =
    [
        (ReadinessCheckArea.DriverOrmCompatibility, "Driver/ORM compatibility"),
        (ReadinessCheckArea.ApplicationVendorCertification, "Application-vendor certification"),
        (ReadinessCheckArea.Extensions, "Extensions/plugins"),
        (ReadinessCheckArea.DeprecatedFeatureUsage, "Deprecated-feature usage"),
        (ReadinessCheckArea.CollationEncoding, "Collation/encoding"),
        (ReadinessCheckArea.OperatingSystem, "Operating system"),
        (ReadinessCheckArea.EditionFeatures, "Edition features"),
        (ReadinessCheckArea.BackupTooling, "Backup tooling"),
        (ReadinessCheckArea.EngineCompatibilityState, "Engine compatibility state (reuses D01's compatibility-level field)"),
    ];

    public async Task<ReadinessChecklist?> GetLatestForInstanceAsync(int instanceId)
    {
        using var db = await factory.CreateDbContextAsync();
        return await db.ReadinessChecklists.AsNoTracking()
            .Include(c => c.Items)
            .Where(c => c.DatabaseInstanceId == instanceId)
            .OrderByDescending(c => c.CreatedAtUtc)
            .FirstOrDefaultAsync();
    }

    public async Task<ReadinessChecklist> CreateChecklistAsync(int instanceId, int? pathAssessmentId, string sourceLabel, string targetLabel, string actorName)
    {
        using var db = await factory.CreateDbContextAsync();
        var instance = await db.DatabaseInstances.FirstAsync(i => i.Id == instanceId);

        var checklist = new ReadinessChecklist
        {
            DatabaseInstanceId = instanceId,
            PathAssessmentId = pathAssessmentId,
            SourceLabel = sourceLabel,
            TargetLabel = targetLabel,
            CreatedAtUtc = DateTime.UtcNow,
            UpdatedAtUtc = DateTime.UtcNow,
            CreatedBy = actorName
        };

        foreach (var (area, label) in StandardAreas)
        {
            checklist.Items.Add(new ReadinessCheckItem { Area = area, Label = label, Status = ReadinessCheckStatus.Unknown });
        }

        db.ReadinessChecklists.Add(checklist);
        await db.SaveChangesAsync();

        await audit.RecordUserActionAsync("", actorName, "ReadinessChecklistCreated", "DatabaseInstance", instanceId.ToString(), instance.Name,
            reason: $"{sourceLabel} -> {targetLabel}");
        return checklist;
    }

    public async Task<ReadinessCheckItem> AddCustomCheckAsync(int checklistId, string label, string actorName)
    {
        using var db = await factory.CreateDbContextAsync();
        var item = new ReadinessCheckItem { ReadinessChecklistId = checklistId, Area = ReadinessCheckArea.Other, Label = label, Status = ReadinessCheckStatus.Unknown };
        db.ReadinessCheckItems.Add(item);
        await db.SaveChangesAsync();
        return item;
    }

    public async Task<bool> UpdateCheckItemAsync(int itemId, ReadinessCheckStatus status, string findings,
        string reviewer, string sourceReference, string targetReference,
        string remediationOwner, string remediationNotes, DateTime? evidenceExpiresAtUtc, string actorName)
    {
        using var db = await factory.CreateDbContextAsync();
        var item = await db.ReadinessCheckItems.Include(i => i.ReadinessChecklist).ThenInclude(c => c!.DatabaseInstance).FirstOrDefaultAsync(i => i.Id == itemId);
        if (item is null)
        {
            return false;
        }

        item.Status = status;
        item.Findings = findings;
        item.Reviewer = reviewer;
        item.ReviewedAtUtc = DateTime.UtcNow;
        item.SourceReference = sourceReference;
        item.TargetReference = targetReference;
        item.RemediationOwner = remediationOwner;
        item.RemediationNotes = remediationNotes;
        item.EvidenceExpiresAtUtc = evidenceExpiresAtUtc;
        // Recording a fresh result is a new review — any earlier invalidation no longer applies to it.
        item.IsInvalidated = false;
        item.InvalidationReason = "";
        item.UpdatedAtUtc = DateTime.UtcNow;
        await db.SaveChangesAsync();

        await audit.RecordUserActionAsync("", actorName, "ReadinessCheckRecorded", "DatabaseInstance",
            item.ReadinessChecklist!.DatabaseInstanceId.ToString(), item.ReadinessChecklist.DatabaseInstance?.Name ?? "",
            reason: $"{item.Label}: {status}");
        return true;
    }

    /// <summary>A changed target invalidates every recorded check; each stays visible but counts as Unknown until re-reviewed.</summary>
    public async Task<bool> UpdateTargetAsync(int checklistId, string newTargetLabel, string actorName)
    {
        using var db = await factory.CreateDbContextAsync();
        var checklist = await db.ReadinessChecklists.Include(c => c.Items).Include(c => c.DatabaseInstance).FirstOrDefaultAsync(c => c.Id == checklistId);
        if (checklist is null)
        {
            return false;
        }

        if (checklist.TargetLabel == newTargetLabel)
        {
            return true;
        }

        var previousTarget = checklist.TargetLabel;
        checklist.TargetLabel = newTargetLabel;
        checklist.UpdatedAtUtc = DateTime.UtcNow;

        foreach (var item in checklist.Items.Where(i => i.Status != ReadinessCheckStatus.Unknown))
        {
            item.IsInvalidated = true;
            item.InvalidationReason = $"Target changed from \"{previousTarget}\" to \"{newTargetLabel}\". Re-review required.";
            item.UpdatedAtUtc = DateTime.UtcNow;
        }

        await db.SaveChangesAsync();

        await audit.RecordUserActionAsync("", actorName, "ReadinessChecklistTargetChanged", "DatabaseInstance",
            checklist.DatabaseInstanceId.ToString(), checklist.DatabaseInstance?.Name ?? "",
            reason: $"Target changed from \"{previousTarget}\" to \"{newTargetLabel}\". Affected checks invalidated.");
        return true;
    }
}
