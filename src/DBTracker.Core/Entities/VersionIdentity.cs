using DBTracker.Core.Enums;

namespace DBTracker.Core.Entities;

/// <summary>Structured version identity parsed from DatabaseInstance.Version, which stays unchanged. Engine version, database compatibility level, and per-node versions (ClusterNodeVersion) are kept separate.</summary>
/// <remarks>An unrecognized string stays Unresolved and is never treated as a known, compliant product.</remarks>
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

    /// <summary>Dedupe state for support-runway notices: the most urgent threshold already notified, and when.</summary>
    public int? LastRunwayNoticeThresholdDays { get; set; }
    public DateTime? LastRunwayNoticeAtUtc { get; set; }

    /// <summary>The milestone date the last deadline notice was about. A different date (a revision, a contract ending, the next phase) restarts the thresholds.</summary>
    public DateTime? LastRunwayNoticeMilestoneUtc { get; set; }

    /// <summary>Deadline, Unknown or NeedsReview (see RunwayNoticeKinds). Empty when no notice has been sent.</summary>
    public string LastRunwayNoticeKind { get; set; } = string.Empty;

    public DateTime UpdatedAtUtc { get; set; } = DateTime.UtcNow;
    public string UpdatedBy { get; set; } = string.Empty;

    public ICollection<ClusterNodeVersion> ClusterNodeVersions { get; set; } = new List<ClusterNodeVersion>();
}
