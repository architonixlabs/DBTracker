namespace DBTracker.Core.Enums;

/// <summary>
/// Whether a raw version string (D01) could be confidently normalized into structured
/// family/major/minor/build fields. Unresolved/NeedsReview are first-class, correct outcomes —
/// an unrecognized or ambiguous version string must never be silently treated as a known, compliant
/// product. Only Resolved identities participate in support-phase, patch-baseline or advisory
/// matching; everything else surfaces as an explicit gap instead.
/// </summary>
public enum VersionResolutionStatus
{
    /// <summary>Confidently parsed into family/major/minor (and usually build).</summary>
    Resolved,

    /// <summary>Could not be parsed at all — empty, garbled, or an engine with no normalization rule.</summary>
    Unresolved,

    /// <summary>Partially parsed but ambiguous (e.g. a MySQL-labelled build that looks like MariaDB) — a person needs to confirm before this is trusted.</summary>
    NeedsReview
}
