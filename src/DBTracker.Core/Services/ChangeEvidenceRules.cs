using System.Text.Json;
using DBTracker.Core.Entities;
using DBTracker.Core.Enums;

namespace DBTracker.Core.Services;

/// <summary>Rules for evidence recorded during and after a change: cutover journal times, the backup or restore that proves post-change
/// recoverability, and the before/after snapshots kept with a validation.</summary>
public static class ChangeEvidenceRules
{
    /// <summary>Clock skew tolerated before a journal time counts as being in the future.</summary>
    public static readonly TimeSpan FutureTolerance = TimeSpan.FromMinutes(5);

    private static readonly JsonSerializerOptions SnapshotJson = new() { WriteIndented = false };

    /// <summary>A journal entry records when something happened, which may be earlier than when it is typed in, but never later than now.</summary>
    public static string? ValidateJournalEntry(string description, DateTime occurredAtUtc, DateTime nowUtc)
    {
        if (string.IsNullOrWhiteSpace(description))
        {
            return "Describe what happened.";
        }
        if (occurredAtUtc > nowUtc + FutureTolerance)
        {
            return "The time it happened can't be in the future.";
        }
        return null;
    }

    /// <summary>Checks the backup or restore linked as post-change evidence. A Passed outcome needs at least one link; every link must belong
    /// to the same instance, be successful, and come from after the change started when that time is known.</summary>
    public static IReadOnlyList<string> ValidateLinkedEvidence(
        EvidenceCheckOutcome outcome,
        int instanceId,
        BackupRecord? linkedBackup,
        RestoreEvent? linkedRestore,
        DateTime? changeStartedUtc)
    {
        var errors = new List<string>();

        if (outcome == EvidenceCheckOutcome.Passed && linkedBackup is null && linkedRestore is null)
        {
            errors.Add("Link the new backup or restore record that shows the check passed.");
        }

        if (linkedBackup is not null)
        {
            if (linkedBackup.DatabaseInstanceId != instanceId)
            {
                errors.Add("The linked backup belongs to a different instance.");
            }
            else if (linkedBackup.Status != BackupStatus.Success)
            {
                errors.Add($"The linked backup is {linkedBackup.Status}, not a successful backup.");
            }
            else if (changeStartedUtc is { } started && linkedBackup.TimestampUtc < started)
            {
                errors.Add($"The linked backup ({linkedBackup.TimestampUtc:yyyy-MM-dd HH:mm} UTC) was taken before the change started ({started:yyyy-MM-dd HH:mm} UTC).");
            }
        }

        if (linkedRestore is not null)
        {
            if (linkedRestore.DatabaseInstanceId != instanceId)
            {
                errors.Add("The linked restore belongs to a different instance.");
            }
            else if (linkedRestore.Outcome != RestoreOutcome.Success)
            {
                errors.Add("The linked restore failed, so it can't prove recoverability.");
            }
            else if (changeStartedUtc is { } started && linkedRestore.TimestampUtc < started)
            {
                errors.Add($"The linked restore ({linkedRestore.TimestampUtc:yyyy-MM-dd HH:mm} UTC) happened before the change started ({started:yyyy-MM-dd HH:mm} UTC).");
            }
        }

        return errors;
    }

    /// <summary>What the inventory said about the instance when the validation was recorded: the declared version and the stored identity.</summary>
    public static string BuildBeforeSnapshot(DatabaseInstance instance, VersionIdentity? identity, DateTime capturedAtUtc) =>
        JsonSerializer.Serialize(new
        {
            capturedAtUtc,
            declaredVersion = instance.Version,
            identity = identity is null ? null : new
            {
                identity.RawVersionText,
                identity.Family,
                identity.Major,
                identity.Minor,
                identity.Build,
                identity.Edition,
                identity.DatabaseCompatibilityLevel,
                resolution = identity.ResolutionStatus.ToString(),
                identity.ObservedAtUtc
            }
        }, SnapshotJson);

    /// <summary>What the reviewer observed after the change, with the evidence records linked to it.</summary>
    public static string BuildAfterSnapshot(ChangeValidation validation, BackupRecord? linkedBackup, RestoreEvent? linkedRestore, DateTime capturedAtUtc) =>
        JsonSerializer.Serialize(new
        {
            capturedAtUtc,
            observedBuild = validation.ObservedTargetBuildText,
            observedCompatibilityLevel = validation.ObservedCompatibilityLevel,
            applicationTests = validation.ApplicationTestsOutcome.ToString(),
            integrityCheck = validation.IntegrityCheckOutcome.ToString(),
            jobsPermissions = validation.JobsPermissionsOutcome.ToString(),
            haReplicationState = validation.HaReplicationStateOutcome.ToString(),
            newBackupRestoreEvidence = validation.NewBackupRestoreEvidenceOutcome.ToString(),
            performanceComparison = validation.PerformanceComparisonNotes,
            linkedBackup = linkedBackup is null ? null : new { linkedBackup.Id, linkedBackup.TimestampUtc, type = linkedBackup.Type.ToString(), status = linkedBackup.Status.ToString() },
            linkedRestore = linkedRestore is null ? null : new { linkedRestore.Id, linkedRestore.TimestampUtc, outcome = linkedRestore.Outcome.ToString() }
        }, SnapshotJson);
}
