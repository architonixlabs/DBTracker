using DBTracker.Core.Entities;
using DBTracker.Core.Enums;

namespace DBTracker.Core.Services;

/// <summary>D11 — the status of the latest rehearsal evidence for one instance/campaign pairing, and why.</summary>
public enum RehearsalReadinessStatus
{
    /// <summary>No rehearsal has ever been recorded for this pairing.</summary>
    Missing,
    /// <summary>A rehearsal exists but is older than its validity window.</summary>
    Stale,
    /// <summary>A rehearsal exists, within its validity window, but at least one required step did not pass.</summary>
    Failed,
    /// <summary>A rehearsal exists, within its validity window, with every required step passed and a documented post-cutover write plan.</summary>
    Passed
}

public record RehearsalReadinessResult(RehearsalReadinessStatus Status, string Reason)
{
    public bool BlocksNormalReadiness => Status != RehearsalReadinessStatus.Passed;
}

/// <summary>
/// Pure (no I/O) evaluation of whether the latest D11 rehearsal for a pairing is current and
/// passing. A missing or stale rehearsal is never silently treated as passing, and a newer-format
/// backup/restore step is never implied to always restore cleanly to an older engine — this
/// evaluator only reports what was actually recorded, it performs no restore itself.
/// </summary>
public static class RehearsalEvidenceEvaluator
{
    public const int DefaultValidityDays = 180;

    public static RehearsalReadinessResult Evaluate(RehearsalRecord? latest, DateTime nowUtc, int defaultValidityDays = DefaultValidityDays)
    {
        if (latest is null)
        {
            return new RehearsalReadinessResult(RehearsalReadinessStatus.Missing, "No rehearsal has been recorded for this source/target pairing yet.");
        }

        var validityDays = latest.ValidityDays ?? defaultValidityDays;
        var expiresAtUtc = latest.PerformedAtUtc.AddDays(validityDays);
        if (expiresAtUtc <= nowUtc)
        {
            return new RehearsalReadinessResult(RehearsalReadinessStatus.Stale,
                $"The last rehearsal ({latest.PerformedAtUtc:yyyy-MM-dd}) is older than its {validityDays}-day validity window — a fresh rehearsal is required.");
        }

        if (latest.BackupRestoreOutcome != EvidenceCheckOutcome.Passed)
        {
            return new RehearsalReadinessResult(RehearsalReadinessStatus.Failed, "The backup-restore step did not pass.");
        }

        if (latest.CorrectnessCheckOutcome != EvidenceCheckOutcome.Passed)
        {
            return new RehearsalReadinessResult(RehearsalReadinessStatus.Failed, "The application correctness check did not pass.");
        }

        if (latest.PerformanceCheckOutcome == EvidenceCheckOutcome.Failed)
        {
            return new RehearsalReadinessResult(RehearsalReadinessStatus.Failed, "The performance comparison did not pass.");
        }

        if (latest.PostCutoverWriteDisposition != PostCutoverWriteDisposition.NotApplicable &&
            string.IsNullOrWhiteSpace(latest.PostCutoverWriteReconciliationPlan))
        {
            return new RehearsalReadinessResult(RehearsalReadinessStatus.Failed,
                "No documented plan for how post-cutover writes would be reconciled or accepted as lost.");
        }

        return new RehearsalReadinessResult(RehearsalReadinessStatus.Passed, "Rehearsal evidence is current and every required step passed.");
    }
}
