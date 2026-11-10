namespace DBTracker.Core.Enums;

public enum AdvisoryVerificationStatus
{
    Unverified,
    InProgress,
    Verified,

    /// <summary>A specific, time-bounded exception was recorded instead of remediation (see D14, future epic) — tracked here as a status, not a full exception workflow yet.</summary>
    Exception
}
