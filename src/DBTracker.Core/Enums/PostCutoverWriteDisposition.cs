namespace DBTracker.Core.Enums;

/// <summary>
/// D11 — how writes that land on the target after cutover (during/after a rehearsal, or for real
/// during a cutover) would be handled if a rollback to the source happened afterward. There is no
/// default that implies writes are automatically safe: NotApplicable must be chosen explicitly, same
/// as Reconciled/Lost — restoring an older backup is never presented as preserving newer writes.
/// </summary>
public enum PostCutoverWriteDisposition
{
    NotApplicable,
    Reconciled,
    Lost
}
