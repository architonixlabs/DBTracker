namespace DBTracker.Core.Enums;

/// <summary>How strongly a backup record's existence has actually been verified.</summary>
public enum EvidenceLevel
{
    /// <summary>A file was found on the shared drive. Presence only; nobody has confirmed it.</summary>
    Discovered,

    /// <summary>A person logged this backup as successful. A report, not file proof.</summary>
    ReportedSuccess,

    /// <summary>A restore has since succeeded from this instance, proving the backup chain recoverable.</summary>
    RestoreTested,

    /// <summary>Loaded from a history import. Kept for context only; a later restore never upgrades it to RestoreTested.</summary>
    Imported
}
