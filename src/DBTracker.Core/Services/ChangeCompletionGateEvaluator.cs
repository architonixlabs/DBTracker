using DBTracker.Core.Entities;
using DBTracker.Core.Enums;

namespace DBTracker.Core.Services;

public record ChangeCompletionGateResult(bool CanComplete, IReadOnlyList<string> BlockingReasons, bool UsedException);

/// <summary>Gate for completing a migration campaign: every required evidence field passed, every required scope signed off, and the latest rehearsal is current and passing. DatabaseInstance.Version is never read here.</summary>
/// <remarks>hasActiveException is the only bypass: a scoped, time-boxed risk acceptance.</remarks>
public static class ChangeCompletionGateEvaluator
{
    public static readonly string[] RequiredSignoffScopes = ["DBA", "Application owner"];

    public static ChangeCompletionGateResult Evaluate(
        ChangeValidation? validation,
        IReadOnlyList<ChangeValidationSignoff> signoffs,
        RehearsalReadinessResult? rehearsalResult,
        bool hasActiveException)
    {
        var reasons = new List<string>();

        if (validation is null)
        {
            reasons.Add("No post-change validation has been recorded for this instance.");
        }
        else
        {
            if (validation.ApplicationTestsOutcome != EvidenceCheckOutcome.Passed)
            {
                reasons.Add("Application tests have not passed.");
            }
            if (validation.IntegrityCheckOutcome != EvidenceCheckOutcome.Passed)
            {
                reasons.Add("Integrity checks have not passed.");
            }
            if (validation.JobsPermissionsOutcome != EvidenceCheckOutcome.Passed)
            {
                reasons.Add("Jobs/permissions review has not passed.");
            }
            if (validation.HaReplicationStateOutcome != EvidenceCheckOutcome.Passed)
            {
                reasons.Add("HA/replication state review has not passed.");
            }
            if (validation.NewBackupRestoreEvidenceOutcome != EvidenceCheckOutcome.Passed)
            {
                reasons.Add("A new successful backup/restore-evidence check has not passed.");
            }
            else if (validation.LinkedBackupRecordId is null && validation.LinkedRestoreEventId is null)
            {
                reasons.Add("Backup/restore evidence is marked Passed, but no backup or restore record is linked to prove it.");
            }
        }

        var approverIdsByScope = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        foreach (var scope in RequiredSignoffScopes)
        {
            var approval = signoffs
                .Where(s => string.Equals(s.ReviewerScope, scope, StringComparison.OrdinalIgnoreCase) && s.Outcome == SignoffOutcome.Approved)
                .OrderByDescending(s => s.SignedAtUtc)
                .FirstOrDefault();
            if (approval is null)
            {
                reasons.Add($"Missing sign-off: {scope}.");
            }
            else if (!string.IsNullOrWhiteSpace(approval.ReviewerUserId))
            {
                approverIdsByScope[scope] = approval.ReviewerUserId;
            }
        }

        // One person approving both required scopes would make the second sign-off meaningless.
        if (approverIdsByScope.Count == RequiredSignoffScopes.Length && approverIdsByScope.Values.Distinct(StringComparer.Ordinal).Count() < approverIdsByScope.Count)
        {
            reasons.Add($"The {string.Join(" and ", RequiredSignoffScopes)} sign-offs must come from different people.");
        }

        if (rehearsalResult is { BlocksNormalReadiness: true })
        {
            reasons.Add($"Rehearsal evidence is not current/passing: {rehearsalResult.Reason}");
        }

        if (reasons.Count == 0)
        {
            return new ChangeCompletionGateResult(true, reasons, false);
        }

        return hasActiveException
            ? new ChangeCompletionGateResult(true, reasons, true)
            : new ChangeCompletionGateResult(false, reasons, false);
    }
}
