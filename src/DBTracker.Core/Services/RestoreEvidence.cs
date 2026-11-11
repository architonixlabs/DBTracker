using DBTracker.Core.Entities;
using DBTracker.Core.Enums;

namespace DBTracker.Core.Services;

/// <summary>Which restore events count as proof that backups are recoverable. Imported history is shown but never counts.</summary>
public static class RestoreEvidence
{
    /// <summary>A successful restore logged in DBTracker. An imported restore is a copied claim, so it never proves recoverability.</summary>
    public static bool CountsAsVerified(RestoreEvent restore) =>
        restore.Outcome == RestoreOutcome.Success && restore.Source != BackupSource.Imported;

    /// <summary>The latest verified successful restore, or null when there is none.</summary>
    public static DateTime? LastVerifiedUtc(IEnumerable<RestoreEvent> restores) =>
        restores.Where(CountsAsVerified).Select(r => (DateTime?)r.TimestampUtc).Max();

    public static bool AnyVerified(IEnumerable<RestoreEvent> restores) => restores.Any(CountsAsVerified);
}
