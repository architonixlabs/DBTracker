using DBTracker.Core.Entities;
using DBTracker.Core.Enums;

namespace DBTracker.Core.Services;

public record RetirementGateResult(string GateKey, string Label, RetirementGateStatus Status, string Detail);

public record RetirementChecklistEvaluation(
    IReadOnlyList<RetirementGateResult> Gates,
    bool AllGatesSatisfied,
    IReadOnlyList<string> BlockingReasons);

/// <summary>Retirement gates computed from evidence the caller gathers (dependencies, chain coverage, copies, certificates, rollback deadline, cost review) plus the checklist's manual facts.</summary>
/// <remarks>Blocked and Unknown are unsatisfied, so proceeding past them needs a scoped exception. NotApplicable counts as
/// satisfied, so a gate returns it only when the absence is proven (the rollback gate: no change was ever validated on
/// this instance), never as a stand-in for "nothing recorded".</remarks>
public static class RetirementChecklistGateEvaluator
{
    private static RetirementGateStatus Worse(RetirementGateStatus a, RetirementGateStatus b)
    {
        // Ordered worst to best: Blocked, Unknown, NotApplicable, Passed.
        int Rank(RetirementGateStatus s) => s switch
        {
            RetirementGateStatus.Blocked => 0,
            RetirementGateStatus.Unknown => 1,
            RetirementGateStatus.NotApplicable => 2,
            RetirementGateStatus.Passed => 3,
            _ => 1
        };
        return Rank(a) <= Rank(b) ? a : b;
    }

    public static RetirementChecklistEvaluation Evaluate(
        RetirementChecklist checklist,
        IReadOnlyCollection<InstanceDependency> dependencies,
        ChainCoverageStatus? chainCoverageStatus,
        bool hasAccessibleVerifiedCopy,
        bool hasAnyCopyLocationsRecorded,
        bool? certificateAccessVerified,
        DateTime? rollbackRetentionDeadlineUtc,
        DateTime nowUtc,
        bool hasChangeValidationHistory = false)
    {
        var gates = new List<RetirementGateResult>
        {
            ConsumersResolvedGate(dependencies),
            JobsReplicasResolvedGate(dependencies),
            FinalRecoverableEvidenceGate(chainCoverageStatus, hasAccessibleVerifiedCopy, hasAnyCopyLocationsRecorded, certificateAccessVerified),
            RollbackWindowClosedGate(rollbackRetentionDeadlineUtc, hasChangeValidationHistory, nowUtc),
            LegalHoldReviewedGate(checklist),
            SignoffGate(checklist),
            CostContractReviewGate(checklist)
        };

        var allSatisfied = gates.All(g => g.Status is RetirementGateStatus.Passed or RetirementGateStatus.NotApplicable);
        var blocking = gates
            .Where(g => g.Status is RetirementGateStatus.Blocked or RetirementGateStatus.Unknown)
            .Select(g => $"{g.Label}: {g.Detail}")
            .ToList();

        return new RetirementChecklistEvaluation(gates, allSatisfied, blocking);
    }

    private static RetirementGateResult ConsumersResolvedGate(IReadOnlyCollection<InstanceDependency> dependencies)
    {
        var consumers = dependencies.Where(d => d.Direction == DependencyDirection.Downstream).ToList();
        if (consumers.Count == 0)
        {
            return new RetirementGateResult("ConsumersResolved", "Downstream consumers moved/confirmed resolved", RetirementGateStatus.Unknown,
                "No downstream-consumer dependencies recorded. Confirm none exist before retiring.");
        }

        var unresolved = consumers.Where(d => !d.IsResolved).Select(d => d.Name).ToList();
        return unresolved.Count == 0
            ? new RetirementGateResult("ConsumersResolved", "Downstream consumers moved/confirmed resolved", RetirementGateStatus.Passed,
                $"All {consumers.Count} recorded downstream consumer(s) resolved.")
            : new RetirementGateResult("ConsumersResolved", "Downstream consumers moved/confirmed resolved", RetirementGateStatus.Blocked,
                $"Unresolved downstream consumer(s): {string.Join(", ", unresolved)}.");
    }

    private static readonly DependencyKind[] JobReplicaKinds = [DependencyKind.Job, DependencyKind.Replica, DependencyKind.LinkedService, DependencyKind.BackupTooling];

    private static RetirementGateResult JobsReplicasResolvedGate(IReadOnlyCollection<InstanceDependency> dependencies)
    {
        var jobsReplicas = dependencies.Where(d => JobReplicaKinds.Contains(d.Kind)).ToList();
        if (jobsReplicas.Count == 0)
        {
            return new RetirementGateResult("JobsReplicasResolved", "Jobs/replicas removed or redirected", RetirementGateStatus.Unknown,
                "No job/replica/linked-service/backup-tooling dependencies recorded. Confirm none still point at this instance.");
        }

        var unresolved = jobsReplicas.Where(d => !d.IsResolved).Select(d => $"{d.Kind}: {d.Name}").ToList();
        return unresolved.Count == 0
            ? new RetirementGateResult("JobsReplicasResolved", "Jobs/replicas removed or redirected", RetirementGateStatus.Passed,
                $"All {jobsReplicas.Count} recorded job/replica dependency(ies) resolved.")
            : new RetirementGateResult("JobsReplicasResolved", "Jobs/replicas removed or redirected", RetirementGateStatus.Blocked,
                $"Still unresolved: {string.Join(", ", unresolved)}.");
    }

    private static RetirementGateResult FinalRecoverableEvidenceGate(
        ChainCoverageStatus? chainCoverageStatus, bool hasAccessibleVerifiedCopy, bool hasAnyCopyLocationsRecorded, bool? certificateAccessVerified)
    {
        var status = chainCoverageStatus switch
        {
            null => RetirementGateStatus.Unknown,
            ChainCoverageStatus.Unknown => RetirementGateStatus.Unknown,
            ChainCoverageStatus.Incomplete => RetirementGateStatus.Blocked,
            ChainCoverageStatus.Complete => RetirementGateStatus.Passed,
            _ => RetirementGateStatus.Unknown
        };
        var details = new List<string> { $"Backup-chain coverage: {chainCoverageStatus?.ToString() ?? "Unknown (no requirements recorded)"}." };

        if (hasAnyCopyLocationsRecorded)
        {
            status = Worse(status, hasAccessibleVerifiedCopy ? RetirementGateStatus.Passed : RetirementGateStatus.Blocked);
            details.Add(hasAccessibleVerifiedCopy
                ? "At least one backup-copy location is currently verified accessible."
                : "No backup-copy location is currently verified accessible.");
        }
        else
        {
            status = Worse(status, RetirementGateStatus.Unknown);
            details.Add("No backup-copy locations recorded.");
        }

        if (certificateAccessVerified is { } verified)
        {
            status = Worse(status, verified ? RetirementGateStatus.Passed : RetirementGateStatus.Blocked);
            details.Add(verified
                ? "Key/certificate backup-restore access is verified."
                : "Key/certificate backup-restore access is not verified.");
        }

        return new RetirementGateResult("FinalRecoverableEvidence", "Final recoverable backup/key-access evidence retained", status, string.Join(" ", details));
    }

    private static RetirementGateResult RollbackWindowClosedGate(DateTime? rollbackRetentionDeadlineUtc, bool hasChangeValidationHistory, DateTime nowUtc)
    {
        if (rollbackRetentionDeadlineUtc is null)
        {
            return hasChangeValidationHistory
                ? new RetirementGateResult("RollbackWindowClosed", "Change rollback window closed", RetirementGateStatus.Unknown,
                    "A change was validated on this instance but no rollback-retention deadline was recorded. Record it on the campaign's validation page.")
                : new RetirementGateResult("RollbackWindowClosed", "Change rollback window closed", RetirementGateStatus.NotApplicable,
                    "No change has been validated on this instance, so there's no rollback window to close.");
        }

        return nowUtc >= rollbackRetentionDeadlineUtc
            ? new RetirementGateResult("RollbackWindowClosed", "Change rollback window closed", RetirementGateStatus.Passed,
                $"Rollback-retention deadline ({rollbackRetentionDeadlineUtc:u}) has passed.")
            : new RetirementGateResult("RollbackWindowClosed", "Change rollback window closed", RetirementGateStatus.Blocked,
                $"Rollback-retention deadline is {rollbackRetentionDeadlineUtc:u}, so the window hasn't closed yet.");
    }

    private static RetirementGateResult LegalHoldReviewedGate(RetirementChecklist checklist) =>
        checklist.LegalHoldReviewed
            ? new RetirementGateResult("LegalHoldReviewed", "Retention/legal-hold obligations reviewed", RetirementGateStatus.Passed, "Reviewed.")
            : new RetirementGateResult("LegalHoldReviewed", "Retention/legal-hold obligations reviewed", RetirementGateStatus.Unknown, "Not yet reviewed.");

    private static RetirementGateResult SignoffGate(RetirementChecklist checklist)
    {
        var missing = new List<string>();
        if (checklist.AppOwnerSignoffAtUtc is null)
        {
            missing.Add("application owner");
        }
        if (checklist.PlatformSignoffAtUtc is null)
        {
            missing.Add("platform");
        }

        return missing.Count == 0
            ? new RetirementGateResult("Signoffs", "Application/platform sign-off", RetirementGateStatus.Passed,
                $"Signed off by {checklist.AppOwnerSignoffBy} (app) and {checklist.PlatformSignoffBy} (platform).")
            : new RetirementGateResult("Signoffs", "Application/platform sign-off", RetirementGateStatus.Unknown,
                $"Missing sign-off: {string.Join(", ", missing)}.");
    }

    private static RetirementGateResult CostContractReviewGate(RetirementChecklist checklist) =>
        checklist.CostContractReviewed
            ? new RetirementGateResult("CostContractReview", "Cost/contract review (D21)", RetirementGateStatus.Passed, "Reviewed.")
            : new RetirementGateResult("CostContractReview", "Cost/contract review (D21)", RetirementGateStatus.Unknown, "Not yet reviewed.");
}
