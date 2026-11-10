namespace DBTracker.Core.Enums;

/// <summary>
/// D08 classification of one source-to-target pairing. The absence of a matching reviewed rule must
/// always classify as Unknown — PathSupportClassifier never defaults an un-ruled pairing to Supported.
/// </summary>
public enum PathSupportStatus
{
    Unknown,
    Supported,
    Blocked
}
