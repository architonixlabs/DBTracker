namespace DBTracker.Core.Enums;

/// <summary>Whether a recovery copy location has been checked, and what was found.</summary>
public enum CopyVerificationStatus
{
    /// <summary>Recorded but never checked. Missing means a check ran and the copy wasn't there.</summary>
    NeverVerified,

    /// <summary>The copy was checked and found present/reachable as of LastVerifiedAtUtc.</summary>
    Accessible,

    /// <summary>A check could not reach or confirm the copy (offline storage, revoked credentials, outage). Not the same claim as Missing.</summary>
    CannotVerify,

    /// <summary>A check was performed and the copy was confirmed absent.</summary>
    Missing
}
