namespace DBTracker.Core.Enums;

/// <summary>Whether a recovery copy location (D17) has actually been checked, and what was found — distinct states, not a single boolean.</summary>
public enum CopyVerificationStatus
{
    /// <summary>Recorded but never actually checked since being added — distinct from Missing, which means a check was performed and the copy wasn't there.</summary>
    NeverVerified,

    /// <summary>The copy was checked and found present/reachable as of LastVerifiedAtUtc.</summary>
    Accessible,

    /// <summary>A check was attempted but could not reach/confirm the copy (e.g. offline storage, revoked credentials, provider outage) — not the same claim as Missing.</summary>
    CannotVerify,

    /// <summary>A check was performed and the copy was confirmed absent.</summary>
    Missing
}
