namespace DBTracker.Core.Enums;

/// <summary>
/// The action a DBA decided on in response to a support-runway deadline (D03). A newer release
/// alone is information, not a mandate — this records which alternative was actually chosen.
/// Today this is just a text/reference field on the decision; full workflow entities for each
/// action type are later-epic scope (D08-D14).
/// </summary>
public enum SupportActionType
{
    Upgrade,
    Patch,
    Replace,
    Retire,
    Exception
}
