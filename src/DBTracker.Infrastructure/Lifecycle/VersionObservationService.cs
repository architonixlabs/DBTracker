using DBTracker.Core.Entities;
using DBTracker.Core.Enums;
using DBTracker.Core.Services;
using DBTracker.Infrastructure.Audit;
using Microsoft.EntityFrameworkCore;

namespace DBTracker.Infrastructure.Lifecycle;

/// <summary>Manual and imported version observations with drift detection against the declared identity. A failed observation never overwrites the identity or archives the instance.</summary>
/// <remarks>RecordFutureCollectorPlaceholderAsync marks where an optional read-only collector would plug in; it is never invoked automatically and makes no network call.</remarks>
public class VersionObservationService(IDbContextFactory<DBTrackerDbContext> factory, AuditRecorder audit)
{
    public async Task<List<VersionObservation>> ListForInstanceAsync(int instanceId)
    {
        using var db = await factory.CreateDbContextAsync();
        return await db.VersionObservations.AsNoTracking()
            .Where(o => o.DatabaseInstanceId == instanceId)
            .OrderByDescending(o => o.ObservedAtUtc)
            .ToListAsync();
    }

    public async Task<VersionObservation> RecordObservationAsync(int instanceId, string rawVersionText, VersionObservationSource source, string observedBy, string notes = "",
        string observedEdition = "", string observedCompatibilityLevel = "")
    {
        using var db = await factory.CreateDbContextAsync();
        var instance = await db.DatabaseInstances.FirstAsync(i => i.Id == instanceId);
        var declared = await db.VersionIdentities.FirstOrDefaultAsync(v => v.DatabaseInstanceId == instanceId);

        var observation = new VersionObservation
        {
            DatabaseInstanceId = instanceId,
            Source = source,
            RawVersionText = rawVersionText,
            Outcome = ObservationOutcome.Success,
            ObservedEdition = observedEdition,
            ObservedCompatibilityLevel = observedCompatibilityLevel,
            ObservedBy = observedBy,
            Notes = notes
        };

        if (declared is not null)
        {
            var drift = VersionDriftDetector.Detect(declared, observation, instance.Engine);
            observation.DriftStatus = drift.Status;
            observation.DriftDetail = string.Join(", ", drift.DifferingFields);
        }
        else
        {
            observation.DriftStatus = DriftStatus.NoObservation;
        }

        db.VersionObservations.Add(observation);
        await db.SaveChangesAsync();

        await audit.RecordUserActionAsync("", observedBy, "VersionObservationRecorded", "DatabaseInstance", instanceId.ToString(), instance.Name,
            reason: observation.DriftStatus == DriftStatus.Drift ? $"Drift detected: {observation.DriftDetail}" : "");
        return observation;
    }

    /// <summary>Records a failed attempt (permission denied or unreachable). Never overwrites VersionIdentity or archives the instance.</summary>
    public async Task<VersionObservation> RecordFailureAsync(int instanceId, ObservationOutcome outcome, string failureDetail, string attemptedBy,
        VersionObservationSource source = VersionObservationSource.ManualEntry, string notes = "")
    {
        if (outcome == ObservationOutcome.Success)
        {
            throw new ArgumentException("Use RecordObservationAsync for a successful observation.", nameof(outcome));
        }

        using var db = await factory.CreateDbContextAsync();
        var instance = await db.DatabaseInstances.FirstAsync(i => i.Id == instanceId);

        var observation = new VersionObservation
        {
            DatabaseInstanceId = instanceId,
            Source = source,
            Outcome = outcome,
            FailureDetail = failureDetail,
            Notes = notes,
            ObservedBy = attemptedBy,
            DriftStatus = DriftStatus.Unknown
        };
        db.VersionObservations.Add(observation);
        await db.SaveChangesAsync();

        await audit.RecordUserActionAsync("", attemptedBy, "VersionObservationFailed", "DatabaseInstance", instanceId.ToString(), instance.Name, reason: failureDetail);
        return observation;
    }

    /// <summary>Builds the version-metadata import preview against current instances and stored observations.</summary>
    public async Task<LifecycleImportPreview<VersionObservation>> PreviewImportAsync(string csvContent)
    {
        using var db = await factory.CreateDbContextAsync();
        var instances = await db.DatabaseInstances.AsNoTracking().Select(i => new { i.Id, i.Name, i.IsArchived }).ToListAsync();
        var byName = instances.GroupBy(i => i.Name, StringComparer.OrdinalIgnoreCase).ToList();
        var importable = byName.Where(g => g.Count() == 1 && !g.First().IsArchived)
            .ToDictionary(g => g.Key, g => g.First().Id, StringComparer.OrdinalIgnoreCase);
        var blocked = byName.Where(g => g.Count() > 1 || g.First().IsArchived)
            .ToDictionary(g => g.Key,
                g => g.Count() > 1 ? $"Name '{g.Key}' matches {g.Count()} instances. Rename one before importing." : $"'{g.Key}' is archived.",
                StringComparer.OrdinalIgnoreCase);

        var stored = await db.VersionObservations.AsNoTracking()
            .Select(o => new { o.DatabaseInstanceId, o.ObservedAtUtc, o.Outcome, o.RawVersionText })
            .ToListAsync();
        var keys = stored.Select(o => VersionObservationCsvImporter.Key(o.DatabaseInstanceId, o.ObservedAtUtc, o.Outcome, o.RawVersionText)).ToHashSet();

        return VersionObservationCsvImporter.ParseForImport(csvContent, importable, blocked, keys);
    }

    /// <summary>Writes the Create rows of an import preview: drift is worked out against each declared identity, failed
    /// reads are stored as failures, and nothing here changes a declared identity or an instance's archived state. Rows
    /// already stored since the preview are skipped.</summary>
    public async Task<(int Created, int Skipped)> ApplyImportAsync(IEnumerable<LifecycleImportRow<VersionObservation>> rows, string actorName)
    {
        using var db = await factory.CreateDbContextAsync();
        var toCreate = rows.Where(r => r.Plan == ImportRowPlan.Create && r.Item is not null).Select(r => r.Item!).ToList();
        var instanceIds = toCreate.Select(o => o.DatabaseInstanceId).Distinct().ToList();
        var instances = await db.DatabaseInstances.AsNoTracking().Where(i => instanceIds.Contains(i.Id) && !i.IsArchived).ToDictionaryAsync(i => i.Id);
        var identities = await db.VersionIdentities.AsNoTracking().Where(v => instanceIds.Contains(v.DatabaseInstanceId)).ToDictionaryAsync(v => v.DatabaseInstanceId);
        var stored = await db.VersionObservations.AsNoTracking().Where(o => instanceIds.Contains(o.DatabaseInstanceId))
            .Select(o => new { o.DatabaseInstanceId, o.ObservedAtUtc, o.Outcome, o.RawVersionText }).ToListAsync();
        var keys = stored.Select(o => VersionObservationCsvImporter.Key(o.DatabaseInstanceId, o.ObservedAtUtc, o.Outcome, o.RawVersionText)).ToHashSet();

        var created = new List<VersionObservation>();
        var skipped = 0;
        foreach (var item in toCreate)
        {
            if (!instances.TryGetValue(item.DatabaseInstanceId, out var instance) ||
                !keys.Add(VersionObservationCsvImporter.Key(item.DatabaseInstanceId, item.ObservedAtUtc, item.Outcome, item.RawVersionText)))
            {
                skipped++;
                continue;
            }

            var observation = new VersionObservation
            {
                DatabaseInstanceId = item.DatabaseInstanceId,
                Source = VersionObservationSource.CsvImport,
                RawVersionText = item.RawVersionText,
                ObservedEdition = item.ObservedEdition,
                ObservedCompatibilityLevel = item.ObservedCompatibilityLevel,
                Outcome = item.Outcome,
                FailureDetail = item.FailureDetail,
                Notes = item.Notes,
                ObservedAtUtc = item.ObservedAtUtc,
                ObservedBy = actorName
            };
            if (identities.TryGetValue(item.DatabaseInstanceId, out var declared))
            {
                var drift = VersionDriftDetector.Detect(declared, observation, instance.Engine);
                observation.DriftStatus = drift.Status;
                observation.DriftDetail = string.Join(", ", drift.DifferingFields);
            }
            else
            {
                observation.DriftStatus = observation.Outcome == ObservationOutcome.Success ? DriftStatus.NoObservation : DriftStatus.Unknown;
            }

            db.VersionObservations.Add(observation);
            created.Add(observation);
        }

        await db.SaveChangesAsync();
        foreach (var o in created)
        {
            var name = instances[o.DatabaseInstanceId].Name;
            await audit.RecordUserActionAsync("", actorName,
                o.Outcome == ObservationOutcome.Success ? "VersionObservationImported" : "VersionObservationFailed",
                "DatabaseInstance", o.DatabaseInstanceId.ToString(), name,
                reason: o.Outcome == ObservationOutcome.Success
                    ? (o.DriftStatus == DriftStatus.Drift ? $"Imported; drift detected: {o.DriftDetail}" : "Imported")
                    : $"Imported failed read ({o.Outcome}): {o.FailureDetail}");
        }
        return (created.Count, skipped);
    }
}
