using DBTracker.Core.Enums;

namespace DBTracker.Core.Entities;

/// <summary>
/// Structured installed-version identity for one instance (D01) — layered on top of
/// DatabaseInstance.Version, which stays exactly as-is and is never replaced. Normalizes the raw
/// text into family/major/minor/build plus edition/distribution/patch-train/hosting context, and
/// keeps three deliberately distinct concepts apart:
///   - HostingEngineVersion: the actual server/engine binary version.
///   - DatabaseCompatibilityLevel: a database-level compatibility setting (e.g. SQL Server's
///     compat level), which can lag behind the engine binary after an in-place upgrade.
///   - Per-node versions in a cluster/replica set (see ClusterNodeVersion) — Epic 17's
///     Region/Cluster/ReplicaSet fields on DatabaseInstance describe *topology*; this describes the
///     *version* observed at each node of that topology, a related but separate concern.
/// An unresolved/unrecognized raw string is a correct, first-class outcome (ResolutionStatus) —
/// it is never silently treated as a known, compliant product.
/// </summary>
public class VersionIdentity
{
    public int Id { get; set; }

    public int DatabaseInstanceId { get; set; }
    public DatabaseInstance? DatabaseInstance { get; set; }

    /// <summary>The exact text this identity was resolved from, captured at resolution time — distinct from DatabaseInstance.Version so editing one never silently "fixes" or invalidates the other.</summary>
    public string RawVersionText { get; set; } = string.Empty;

    public VersionResolutionStatus ResolutionStatus { get; set; } = VersionResolutionStatus.Unresolved;
    public string UnresolvedReason { get; set; } = string.Empty;

    public string Family { get; set; } = string.Empty;
    public int? Major { get; set; }
    public int? Minor { get; set; }
    public string Build { get; set; } = string.Empty;

    public string Edition { get; set; } = string.Empty;
    public string Distribution { get; set; } = string.Empty;
    public string PatchTrain { get; set; } = string.Empty;

    public string HostingProvider { get; set; } = string.Empty;
    public string HostingContext { get; set; } = string.Empty;

    /// <summary>The actual server/engine binary version — distinct from DatabaseCompatibilityLevel below.</summary>
    public string HostingEngineVersion { get; set; } = string.Empty;

    /// <summary>A database-level compatibility setting (e.g. SQL Server compat level "150") — can lag behind HostingEngineVersion after an in-place upgrade.</summary>
    public string DatabaseCompatibilityLevel { get; set; } = string.Empty;

    public VersionObservationSource ObservationSource { get; set; } = VersionObservationSource.ManualEntry;
    public DateTime ObservedAtUtc { get; set; } = DateTime.UtcNow;

    public bool IsAttested { get; set; }
    public string AttestedBy { get; set; } = string.Empty;
    public DateTime? AttestedAtUtc { get; set; }

    /// <summary>Dedupe state for D03's escalating support-runway notices — the smallest (most urgent) threshold already notified for this instance, and when.</summary>
    public int? LastRunwayNoticeThresholdDays { get; set; }
    public DateTime? LastRunwayNoticeAtUtc { get; set; }

    public DateTime UpdatedAtUtc { get; set; } = DateTime.UtcNow;
    public string UpdatedBy { get; set; } = string.Empty;

    public ICollection<ClusterNodeVersion> ClusterNodeVersions { get; set; } = new List<ClusterNodeVersion>();
}
