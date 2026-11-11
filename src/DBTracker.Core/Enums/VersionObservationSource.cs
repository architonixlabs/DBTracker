namespace DBTracker.Core.Enums;

/// <summary>Where a version observation came from. Manual attestation and metadata import are the supported paths.</summary>
public enum VersionObservationSource
{
    ManualEntry,
    CsvImport,

    /// <summary>A person explicitly attested this is correct (distinct from just entering text).</summary>
    Attested,

    /// <summary>Not implemented. Reserved for an explicitly configured, read-only, least-privilege collector.</summary>
    FutureCollector
}
