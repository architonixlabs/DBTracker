namespace DBTracker.Core.Enums;

/// <summary>The action a DBA chose in response to a support-runway deadline. A newer release alone is information, not a mandate.</summary>
public enum SupportActionType
{
    Upgrade,
    Patch,
    Replace,
    Retire,
    Exception
}
