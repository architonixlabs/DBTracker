namespace DBTracker.Core.Enums;

/// <summary>Areas an upgrade-readiness checklist covers, applied per engine as relevant.</summary>
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
    /// <summary>Checks VersionIdentity.DatabaseCompatibilityLevel.</summary>
    EngineCompatibilityState,
    Other
}
