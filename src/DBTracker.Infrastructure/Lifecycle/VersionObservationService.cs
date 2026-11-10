using DBTracker.Core.Entities;
using DBTracker.Core.Enums;
using DBTracker.Core.Services;
using DBTracker.Infrastructure.Audit;
using Microsoft.EntityFrameworkCore;

namespace DBTracker.Infrastructure.Lifecycle;

/// <summary>
/// D06 — manual/import version-metadata observations and drift detection against the declared
/// VersionIdentity. Manual attestation (VersionIdentityService.AttestAsync) and this documented
/// import path are the first-class, shipped ways to get metadata in; RecordFutureCollectorPlaceholderAsync
/// exists only to show where an explicitly-configured, read-only, least-privilege, bounded-schedule
/// collector would plug in later — it is never invoked automatically and makes no network call.
/// A permission-failure observation never overwrites the declared VersionIdentity and never touches
/// DatabaseInstance.IsArchived.
/// </summary>
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

    public async Task<VersionObservation> RecordObservationAsync(int instanceId, string rawVersionText, VersionObservationSource source, string observedBy, string notes = "")
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

    /// <summary>A failed observation attempt — permission denied/unreachable. Recorded honestly; never overwrites VersionIdentity and never marks the instance retired/archived.</summary>
    public async Task<VersionObservation> RecordFailureAsync(int instanceId, ObservationOutcome outcome, string failureDetail, string attemptedBy)
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
            Source = VersionObservationSource.FutureCollector,
            Outcome = outcome,
            FailureDetail = failureDetail,
            ObservedBy = attemptedBy,
            DriftStatus = DriftStatus.Unknown
        };
        db.VersionObservations.Add(observation);
        await db.SaveChangesAsync();

        await audit.RecordUserActionAsync("", attemptedBy, "VersionObservationFailed", "DatabaseInstance", instanceId.ToString(), instance.Name, reason: failureDetail);
        return observation;
    }
}
