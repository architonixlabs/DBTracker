using DBTracker.Core.Entities;
using DBTracker.Core.Enums;
using DBTracker.Core.Services;
using DBTracker.Infrastructure.Audit;
using DBTracker.Infrastructure.Identity;
using Microsoft.EntityFrameworkCore;

namespace DBTracker.Infrastructure.Recovery;

public record MaintenanceEvidenceRow(MaintenanceEvidenceRecord Record, MaintenanceEvidenceStatus Status);

/// <summary>Integrity, HA and maintenance-job evidence, never read by backup health or vice versa. A non-manual Source requires SourceReference, checked here too because imports land here.</summary>
public class MaintenanceEvidenceService(IDbContextFactory<DBTrackerDbContext> factory, AuditRecorder audit)
{
    private static readonly string[] EditorRoles = [Roles.Admin, Roles.Dba];

    public async Task<List<MaintenanceEvidenceRow>> ListForInstanceAsync(int instanceId)
    {
        using var db = await factory.CreateDbContextAsync();
        var records = await db.MaintenanceEvidenceRecords.AsNoTracking()
            .Include(r => r.RelatedDependency)
            .Where(r => r.DatabaseInstanceId == instanceId)
            .OrderByDescending(r => r.RanAtUtc)
            .ToListAsync();

        var now = DateTime.UtcNow;
        return records.Select(r => new MaintenanceEvidenceRow(r, MaintenanceEvidenceStalenessClassifier.Classify(r, now))).ToList();
    }

    /// <summary>Each required check with its status from the results on record. A requirement nothing matches is Missing.</summary>
    public async Task<IReadOnlyList<MaintenanceRequirementStatus>> EvaluateRequirementsAsync(int instanceId)
    {
        using var db = await factory.CreateDbContextAsync();
        var requirements = await db.MaintenanceCheckRequirements.AsNoTracking().Where(r => r.DatabaseInstanceId == instanceId).ToListAsync();
        var records = await db.MaintenanceEvidenceRecords.AsNoTracking().Include(r => r.RelatedDependency).Where(r => r.DatabaseInstanceId == instanceId).ToListAsync();
        return MaintenanceRequirementEvaluator.Evaluate(requirements, records, DateTime.UtcNow);
    }

    /// <param name="enteredTimeZone">The zone the person stated the run time in, kept in the audit entry.</param>
    public async Task<(bool Ok, string Error)> RecordAsync(MaintenanceEvidenceRecord record, string actorName, string enteredTimeZone = "")
    {
        if (record.Source != MaintenanceEvidenceSource.ManuallyEntered && string.IsNullOrWhiteSpace(record.SourceReference))
        {
            return (false, "A reviewed external monitoring or ticket link requires a source reference.");
        }
        if (record.RanAtUtc is { } ranAt && ranAt > DateTime.UtcNow + EvidenceTimeTolerance)
        {
            return (false, "The run time can't be in the future.");
        }

        using var db = await factory.CreateDbContextAsync();
        record.Notes = SecretLeakGuard.Redact(record.Notes);
        record.SourceReference = SecretLeakGuard.Redact(record.SourceReference);
        record.RecordedAtUtc = DateTime.UtcNow;
        record.RecordedBy = actorName;
        db.MaintenanceEvidenceRecords.Add(record);
        await db.SaveChangesAsync();

        var instance = await db.DatabaseInstances.FirstAsync(i => i.Id == record.DatabaseInstanceId);
        await audit.RecordUserActionAsync("", actorName, "MaintenanceEvidenceRecorded", "DatabaseInstance", record.DatabaseInstanceId.ToString(), instance.Name,
            reason: $"{record.CheckType}: {record.Outcome} ({record.Source})", afterJson: EvidenceAuditSnapshot.Maintenance(record, enteredTimeZone));
        return (true, "");
    }

    private static readonly TimeSpan EvidenceTimeTolerance = TimeSpan.FromMinutes(5);

    /// <summary>Adds or updates the requirement for one check type (and optional node) on an instance.</summary>
    public async Task<(bool Ok, string Error)> SetRequirementAsync(int instanceId, MaintenanceCheckType checkType, string nodeLabel, int expectedIntervalHours,
        string notes, string actorName, IReadOnlyCollection<string> actorRoles)
    {
        if (!actorRoles.Any(EditorRoles.Contains))
        {
            return (false, "Only Admin and DBA users can change required checks.");
        }
        if (expectedIntervalHours < 1)
        {
            return (false, "The expected interval must be at least 1 hour.");
        }
        var label = nodeLabel.Trim();
        if (label.Length > 200)
        {
            return (false, "Keep the node or deployment label under 200 characters.");
        }

        using var db = await factory.CreateDbContextAsync();
        var instance = await db.DatabaseInstances.FirstOrDefaultAsync(i => i.Id == instanceId);
        if (instance is null)
        {
            return (false, "Instance not found.");
        }

        var existing = await db.MaintenanceCheckRequirements.FirstOrDefaultAsync(r =>
            r.DatabaseInstanceId == instanceId && r.CheckType == checkType && r.NodeOrDeploymentLabel == label);
        if (existing is null)
        {
            db.MaintenanceCheckRequirements.Add(new MaintenanceCheckRequirement
            {
                DatabaseInstanceId = instanceId,
                CheckType = checkType,
                NodeOrDeploymentLabel = label,
                ExpectedIntervalHours = expectedIntervalHours,
                Notes = notes.Trim(),
                CreatedBy = actorName
            });
        }
        else
        {
            existing.ExpectedIntervalHours = expectedIntervalHours;
            existing.Notes = notes.Trim();
        }
        await db.SaveChangesAsync();

        await audit.RecordUserActionAsync("", actorName, existing is null ? "MaintenanceRequirementAdded" : "MaintenanceRequirementUpdated",
            "DatabaseInstance", instanceId.ToString(), instance.Name,
            reason: $"{checkType}{(label.Length > 0 ? $" on {label}" : "")} every {expectedIntervalHours}h");
        return (true, "");
    }

    public async Task<(bool Ok, string Error)> RemoveRequirementAsync(int requirementId, string actorName, IReadOnlyCollection<string> actorRoles)
    {
        if (!actorRoles.Any(EditorRoles.Contains))
        {
            return (false, "Only Admin and DBA users can change required checks.");
        }

        using var db = await factory.CreateDbContextAsync();
        var requirement = await db.MaintenanceCheckRequirements.Include(r => r.DatabaseInstance).FirstOrDefaultAsync(r => r.Id == requirementId);
        if (requirement is null)
        {
            return (false, "Requirement not found.");
        }

        db.MaintenanceCheckRequirements.Remove(requirement);
        await db.SaveChangesAsync();

        await audit.RecordUserActionAsync("", actorName, "MaintenanceRequirementRemoved", "DatabaseInstance", requirement.DatabaseInstanceId.ToString(),
            requirement.DatabaseInstance?.Name ?? "", reason: $"{requirement.CheckType}{(requirement.NodeOrDeploymentLabel.Length > 0 ? $" on {requirement.NodeOrDeploymentLabel}" : "")}");
        return (true, "");
    }

    /// <summary>Parses an import file against the current inventory and recorded results. Nothing is written.</summary>
    public async Task<EvidenceImportPreview<MaintenanceEvidenceRecord>> PreviewImportAsync(string csvContent)
    {
        using var db = await factory.CreateDbContextAsync();
        var (idsByName, blocked) = await EvidenceImportLookup.LoadInstancesAsync(db);
        var ids = idsByName.Values.ToList();
        var existing = await db.MaintenanceEvidenceRecords.AsNoTracking().Where(r => ids.Contains(r.DatabaseInstanceId)).ToListAsync();
        return MaintenanceEvidenceCsvImporter.Parse(csvContent, idsByName, blocked, existing, DateTime.UtcNow);
    }

    /// <summary>Re-parses the file at apply time, so anything recorded since the preview is skipped, and records only its Create rows in one save.</summary>
    public async Task<(bool Ok, string Message, int Created)> ApplyImportAsync(string csvContent, string fileName, string actorUserId, string actorName,
        IReadOnlyCollection<string> actorRoles)
    {
        if (!actorRoles.Any(EditorRoles.Contains))
        {
            return (false, "Only Admin and DBA users can import results.", 0);
        }

        var preview = await PreviewImportAsync(csvContent);
        var creates = preview.Rows.Where(r => r.Plan == EvidenceImportRowPlan.Create && r.Record is not null).Select(r => r.Record!).ToList();
        if (creates.Count == 0)
        {
            return (false, "Nothing new to import.", 0);
        }

        using var db = await factory.CreateDbContextAsync();
        var now = DateTime.UtcNow;
        foreach (var record in creates)
        {
            record.RecordedAtUtc = now;
            record.RecordedBy = actorName;
        }
        db.MaintenanceEvidenceRecords.AddRange(creates);
        await db.SaveChangesAsync();

        await audit.RecordUserActionAsync(actorUserId, actorName, "MaintenanceEvidenceCsvImported", "CsvImport", "", fileName,
            reason: $"{creates.Count} recorded, {preview.SkipCount} already on record, {preview.ConflictCount} conflicting, {preview.Errors.Count} error row(s)");
        return (true, $"{creates.Count} result(s) recorded.", creates.Count);
    }
}

/// <summary>Instance lookup shared by the evidence imports: unique, non-archived names import; archived or duplicated names are refused with a reason.</summary>
internal static class EvidenceImportLookup
{
    public static async Task<(Dictionary<string, int> IdsByName, Dictionary<string, string> Blocked)> LoadInstancesAsync(DBTrackerDbContext db)
    {
        var all = await db.DatabaseInstances.AsNoTracking().Select(i => new { i.Id, i.Name, i.IsArchived }).ToListAsync();
        var groups = all.GroupBy(i => i.Name, StringComparer.OrdinalIgnoreCase).ToList();
        var idsByName = groups.Where(g => g.Count() == 1 && !g.First().IsArchived)
            .ToDictionary(g => g.Key, g => g.First().Id, StringComparer.OrdinalIgnoreCase);
        var blocked = groups.Where(g => g.Count() > 1 || g.First().IsArchived)
            .ToDictionary(g => g.Key,
                g => g.Count() > 1
                    ? $"'{g.Key}' matches {g.Count()} instances (including archived ones), so the row can't be matched safely."
                    : $"'{g.Key}' is archived. Unarchive it before importing evidence for it.",
                StringComparer.OrdinalIgnoreCase);
        return (idsByName, blocked);
    }
}
