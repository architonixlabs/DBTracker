namespace DBTracker.Core.Enums;

/// <summary>How strongly a backup record's existence has actually been verified.</summary>
public enum EvidenceLevel
{
    /// <summary>A file was found on the shared drive — presence only, not confirmed by anyone.</summary>
    Discovered,

    /// <summary>A person manually logged this backup as successful — a report, not file proof.</summary>
    ReportedSuccess,

    /// <summary>A restore has since succeeded from this instance, proving the backup chain recoverable.</summary>
    RestoreTested
}
