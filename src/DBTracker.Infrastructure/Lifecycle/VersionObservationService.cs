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

    /// <summary>Records a failed attempt (permission denied or unreachable). Never overwrites VersionIdentity or archives the instance.</summary>
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
