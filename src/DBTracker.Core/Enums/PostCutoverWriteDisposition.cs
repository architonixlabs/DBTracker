namespace DBTracker.Core.Enums;

/// <summary>How writes landing on the target after cutover would be handled on rollback. There is no safe default; restoring an older backup never preserves newer writes.</summary>
public enum PostCutoverWriteDisposition
{
    NotApplicable,
    Reconciled,
    Lost
}
