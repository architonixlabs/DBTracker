using DBTracker.Core.Entities;
using DBTracker.Core.Enums;
using DBTracker.Core.Services;
using DBTracker.Infrastructure.Audit;
using Microsoft.EntityFrameworkCore;

namespace DBTracker.Infrastructure.Lifecycle;

/// <summary>Structured version identity per instance, alongside the untouched DatabaseInstance.Version. Writes are audited, and unresolvable text is stored as unresolved.</summary>
public class VersionIdentityService(IDbContextFactory<DBTrackerDbContext> factory, AuditRecorder audit)
{
    public async Task<VersionIdentity?> GetByInstanceAsync(int instanceId)
    {
        using var db = await factory.CreateDbContextAsync();
        return await db.VersionIdentities.AsNoTracking()
            .Include(v => v.ClusterNodeVersions)
            .FirstOrDefaultAsync(v => v.DatabaseInstanceId == instanceId);
    }

    /// <summary>Re-parses the instance's Version field into a stored identity.</summary>
    public async Task<VersionIdentity> ResolveFromInstanceVersionAsync(int instanceId, string actorName)
    {
        using var db = await factory.CreateDbContextAsync();
        var instance = await db.DatabaseInstances.FirstAsync(i => i.Id == instanceId);
        return await SaveRawTextAsync(db, instance, instance.Version, VersionObservationSource.ManualEntry, actorName);
    }

    /// <summary>Manual entry of a raw version string, possibly more precise than DatabaseInstance.Version, including unresolvable text.</summary>
    public async Task<VersionIdentity> RecordManualRawVersionAsync(int instanceId, string rawVersionText, string actorName)
    {
        using var db = await factory.CreateDbContextAsync();
        var instance = await db.DatabaseInstances.FirstAsync(i => i.Id == instanceId);
        return await SaveRawTextAsync(db, instance, rawVersionText, VersionObservationSource.ManualEntry, actorName);
    }

    private async Task<VersionIdentity> SaveRawTextAsync(DBTrackerDbContext db, DatabaseInstance instance, string rawVersionText, VersionObservationSource source, string actorName)
    {
        var parsed = VersionIdentityParser.Parse(instance.Engine, rawVersionText);
        var existing = await db.VersionIdentities.FirstOrDefaultAsync(v => v.DatabaseInstanceId == instance.Id);
        var isNew = existing is null;
        var identity = existing ?? new VersionIdentity { DatabaseInstanceId = instance.Id };

        identity.RawVersionText = (rawVersionText ?? "").Trim();
        identity.ResolutionStatus = parsed.Status;
        identity.UnresolvedReason = parsed.UnresolvedReason;
        identity.Family = parsed.Family;
        identity.Major = parsed.Major;
        identity.Minor = parsed.Minor;
        identity.Build = parsed.Build;
        if (string.IsNullOrWhiteSpace(identity.Distribution))
        {
            identity.Distribution = parsed.Distribution;
        }
        if (string.IsNullOrWhiteSpace(identity.PatchTrain))
        {
            identity.PatchTrain = parsed.PatchTrain;
        }
        identity.ObservationSource = source;
        identity.ObservedAtUtc = DateTime.UtcNow;
        identity.UpdatedAtUtc = DateTime.UtcNow;
        identity.UpdatedBy = actorName;

        // An attestation covers a specific value, so changing the raw text resets it.
        if (!isNew && existing!.RawVersionText != identity.RawVersionText)
        {
            identity.IsAttested = false;
            identity.AttestedBy = "";
            identity.AttestedAtUtc = null;
        }

        if (isNew)
        {
            db.VersionIdentities.Add(identity);
        }

        await db.SaveChangesAsync();
        await audit.RecordUserActionAsync("", actorName, "VersionIdentityUpdated", "DatabaseInstance", instance.Id.ToString(), instance.Name,
            reason: parsed.Status == VersionResolutionStatus.Resolved ? "" : parsed.UnresolvedReason,
            afterJson: $"{{\"raw\":\"{identity.RawVersionText}\",\"status\":\"{identity.ResolutionStatus}\",\"family\":\"{identity.Family}\",\"major\":{identity.Major?.ToString() ?? "null"},\"minor\":{identity.Minor?.ToString() ?? "null"}}}");

        return identity;
    }

    /// <summary>Sets fields a parser can't infer: edition, distribution, patch train, hosting, compatibility level.</summary>
    public async Task SetStructuredFieldsAsync(int instanceId, string edition, string distribution, string patchTrain,
        string hostingProvider, string hostingContext, string hostingEngineVersion, string databaseCompatibilityLevel, string actorName)
    {
        using var db = await factory.CreateDbContextAsync();
        var identity = await db.VersionIdentities.FirstOrDefaultAsync(v => v.DatabaseInstanceId == instanceId);
        if (identity is null)
        {
            return;
        }

        identity.Edition = edition;
        identity.Distribution = distribution;
        identity.PatchTrain = patchTrain;
        identity.HostingProvider = hostingProvider;
        identity.HostingContext = hostingContext;
        identity.HostingEngineVersion = hostingEngineVersion;
        identity.DatabaseCompatibilityLevel = databaseCompatibilityLevel;
        identity.UpdatedAtUtc = DateTime.UtcNow;
        identity.UpdatedBy = actorName;
        await db.SaveChangesAsync();

        var instance = await db.DatabaseInstances.FirstAsync(i => i.Id == instanceId);
        await audit.RecordUserActionAsync("", actorName, "VersionIdentityMetadataUpdated", "DatabaseInstance", instanceId.ToString(), instance.Name);
    }

    /// <summary>Records that a person attested the stored identity is correct.</summary>
    public async Task<bool> AttestAsync(int instanceId, string attestedBy)
    {
        using var db = await factory.CreateDbContextAsync();
        var identity = await db.VersionIdentities.FirstOrDefaultAsync(v => v.DatabaseInstanceId == instanceId);
        if (identity is null)
        {
            return false;
        }

        identity.IsAttested = true;
        identity.AttestedBy = attestedBy;
        identity.AttestedAtUtc = DateTime.UtcNow;
        await db.SaveChangesAsync();

        var instance = await db.DatabaseInstances.FirstAsync(i => i.Id == instanceId);
        await audit.RecordUserActionAsync("", attestedBy, "VersionIdentityAttested", "DatabaseInstance", instanceId.ToString(), instance.Name);
        return true;
    }

    public async Task<List<ClusterNodeVersion>> ListClusterNodesAsync(int instanceId)
    {
        using var db = await factory.CreateDbContextAsync();
        return await db.ClusterNodeVersions.AsNoTracking()
            .Where(n => n.DatabaseInstanceId == instanceId)
            .OrderBy(n => n.NodeName)
            .ToListAsync();
    }

    public async Task AddClusterNodeAsync(int instanceId, string nodeName, string rawVersionText, string notes, string actorName)
    {
        using var db = await factory.CreateDbContextAsync();
        var instance = await db.DatabaseInstances.FirstAsync(i => i.Id == instanceId);
        var identity = await db.VersionIdentities.FirstOrDefaultAsync(v => v.DatabaseInstanceId == instanceId);
        var parsed = VersionIdentityParser.Parse(instance.Engine, rawVersionText);

        db.ClusterNodeVersions.Add(new ClusterNodeVersion
        {
            DatabaseInstanceId = instanceId,
            VersionIdentityId = identity?.Id,
            NodeName = nodeName,
            RawVersionText = rawVersionText,
            ResolutionStatus = parsed.Status,
            Family = parsed.Family,
            Major = parsed.Major,
            Minor = parsed.Minor,
            Build = parsed.Build,
            Notes = notes
        });
        await db.SaveChangesAsync();
        await audit.RecordUserActionAsync("", actorName, "ClusterNodeVersionRecorded", "DatabaseInstance", instanceId.ToString(), $"{instance.Name} / {nodeName}");
    }

    public async Task RemoveClusterNodeAsync(int nodeId)
    {
        using var db = await factory.CreateDbContextAsync();
        await db.ClusterNodeVersions.Where(n => n.Id == nodeId).ExecuteDeleteAsync();
    }
}
