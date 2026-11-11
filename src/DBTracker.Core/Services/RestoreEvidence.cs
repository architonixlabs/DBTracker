using System.Linq.Expressions;
using DBTracker.Core.Entities;
using DBTracker.Core.Enums;

namespace DBTracker.Core.Services;

/// <summary>Which restore events count as proof that backups are recoverable. Imported history is shown but never counts.
/// The outcome used is the effective one: the newest correction's outcome when a restore was corrected, otherwise the original.</summary>
/// <remarks>Corrections are read from <see cref="RestoreEvent.Corrections"/>, so callers must load them with the restores.</remarks>
public static class RestoreEvidence
{
    /// <summary>A successful restore logged in DBTracker. An imported restore is a copied claim, so it never proves recoverability.</summary>
    public static bool CountsAsVerified(RestoreEvent restore) => CountsAsVerified(restore, null);

    /// <param name="correctionsKnownAsOfUtc">Only corrections made by then apply, for an as-of view of what was known. Null applies all.</param>
    public static bool CountsAsVerified(RestoreEvent restore, DateTime? correctionsKnownAsOfUtc) =>
        restore.Source != BackupSource.Imported && EffectiveOutcome(restore, correctionsKnownAsOfUtc) == RestoreOutcome.Success;

    public static RestoreOutcome EffectiveOutcome(RestoreEvent restore, DateTime? correctionsKnownAsOfUtc = null)
    {
        var corrections = correctionsKnownAsOfUtc is { } asOf
            ? restore.Corrections.Where(c => c.CorrectedAtUtc <= asOf).ToList()
            : restore.Corrections.ToList();
        return EvidenceCorrectionResolver.EffectiveOutcome(restore, corrections);
    }

    /// <summary>The latest verified successful restore, or null when there is none.</summary>
    public static DateTime? LastVerifiedUtc(IEnumerable<RestoreEvent> restores, DateTime? correctionsKnownAsOfUtc = null) =>
        restores.Where(r => CountsAsVerified(r, correctionsKnownAsOfUtc)).Select(r => (DateTime?)r.TimestampUtc).Max();

    public static bool AnyVerified(IEnumerable<RestoreEvent> restores) => restores.Any(CountsAsVerified);

    /// <summary>The same rule as <see cref="CountsAsVerified(RestoreEvent)"/> for database queries: the newest correction that set
    /// an outcome wins, otherwise the original outcome.</summary>
    public static readonly Expression<Func<RestoreEvent, bool>> IsVerified = r =>
        r.Source != BackupSource.Imported &&
        (r.Corrections
            .Where(c => c.NewOutcome != null)
            .OrderByDescending(c => c.CorrectedAtUtc).ThenByDescending(c => c.Id)
            .Select(c => c.NewOutcome)
            .FirstOrDefault() ?? r.Outcome) == RestoreOutcome.Success;
}
