using DBTracker.Core.Entities;
using DBTracker.Core.Enums;

namespace DBTracker.Core.Services;

public record ChangeCompletionGateResult(bool CanComplete, IReadOnlyList<string> BlockingReasons, bool UsedException);

/// <summary>
/// Pure (no I/O) gate for completing a D10 MigrationCampaign (reaching CampaignState.Completed).
/// A ChangeValidation record existing is not enough — every required evidence field on it must have
/// actually passed, every required reviewer scope must have signed off Approved, and (per D11) the
/// latest rehearsal for the same instance must still be current and passing. Editing
/// DatabaseInstance.Version is not read anywhere in this evaluator — it cannot, by construction,
/// complete a change on its own. hasActiveException is the single D14 bypass: an explicit, scoped,
/// time-boxed risk acceptance rather than a silent override of any of the checks above.
/// </summary>
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
        }

        foreach (var scope in RequiredSignoffScopes)
        {
            var hasApproval = signoffs.Any(s => string.Equals(s.ReviewerScope, scope, StringComparison.OrdinalIgnoreCase) && s.Outcome == SignoffOutcome.Approved);
            if (!hasApproval)
            {
                reasons.Add($"Missing sign-off: {scope}.");
            }
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
