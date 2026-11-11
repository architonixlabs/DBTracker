namespace DBTracker.Core.Enums;

/// <summary>Whether a raw version string normalized confidently. Only Resolved identities take part in support-phase, patch-baseline or advisory matching; the rest surface as explicit gaps.</summary>
public enum VersionResolutionStatus
{
    /// <summary>Confidently parsed into family/major/minor (and usually build).</summary>
    Resolved,

    /// <summary>Could not be parsed: empty, garbled, or an engine with no normalization rule.</summary>
    Unresolved,

    /// <summary>Partly parsed but ambiguous (e.g. a MySQL-labelled build that looks like MariaDB); a person must confirm it.</summary>
    NeedsReview
}
