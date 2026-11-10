namespace DBTracker.Core.Enums;

/// <summary>
/// Shared outcome for one discrete evidence check across D11 (rehearsal) and D13 (post-change
/// validation) — correctness/performance/application-tests/integrity/jobs-permissions/HA-replication/
/// backup-restore. NotPerformed is the default and is never treated as Passed anywhere that reads it.
/// </summary>
public enum EvidenceCheckOutcome
{
    NotPerformed,
    Passed,
    Failed
}
