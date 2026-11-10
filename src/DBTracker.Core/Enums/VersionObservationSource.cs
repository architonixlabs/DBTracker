namespace DBTracker.Core.Enums;

/// <summary>
/// Where a version observation (D01/D06) came from. Manual attestation and documented metadata
/// import are the first-class, shipped paths. FutureCollector is a clearly-labelled extension point
/// only — D06 explicitly does not implement a real network/credentialed collector in this pass.
/// </summary>
public enum VersionObservationSource
{
    ManualEntry,
    CsvImport,

    /// <summary>A person explicitly attested this is correct (distinct from just entering text).</summary>
    Attested,

    /// <summary>Not implemented — a placeholder for an explicitly-configured, read-only, least-privilege, bounded-schedule optional collector described in D06 but not built in this pass.</summary>
    FutureCollector
}
