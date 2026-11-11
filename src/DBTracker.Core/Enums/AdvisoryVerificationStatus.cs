namespace DBTracker.Core.Enums;

public enum AdvisoryVerificationStatus
{
    Unverified,
    InProgress,
    Verified,

    /// <summary>A time-bounded exception was recorded instead of remediation.</summary>
    Exception
}
