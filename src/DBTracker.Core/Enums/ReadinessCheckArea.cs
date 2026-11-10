namespace DBTracker.Core.Enums;

/// <summary>D09 — the fixed set of areas an upgrade-readiness checklist covers, applied as relevant per engine.</summary>
public enum ReadinessCheckArea
{
    DriverOrmCompatibility,
    ApplicationVendorCertification,
    Extensions,
    DeprecatedFeatureUsage,
    CollationEncoding,
    OperatingSystem,
    EditionFeatures,
    BackupTooling,
    /// <summary>Reuses D01's compatibility-level concept (VersionIdentity.DatabaseCompatibilityLevel), not a separate rule.</summary>
    EngineCompatibilityState,
    Other
}
