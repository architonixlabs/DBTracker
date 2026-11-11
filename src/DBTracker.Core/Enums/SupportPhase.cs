namespace DBTracker.Core.Enums;

/// <summary>A release's standard support phase relative to today. Unknown when the catalogue has no usable milestones; never defaulted to Mainstream.</summary>
public enum SupportPhase
{
    Mainstream,
    Extended,

    /// <summary>Extended Security Updates / paid extended-support coverage beyond the standard lifecycle.</summary>
    Esu,
    EndOfLife,
    Unknown
}
