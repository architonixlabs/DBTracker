namespace DBTracker.Core.Enums;

/// <summary>A product release's standard support phase (D03) relative to today. Unknown is a correct, first-class outcome when the catalogue has no usable milestone data — never defaulted to Mainstream.</summary>
public enum SupportPhase
{
    Mainstream,
    Extended,

    /// <summary>Extended Security Updates / paid extended-support coverage beyond the standard lifecycle.</summary>
    Esu,
    EndOfLife,
    Unknown
}
