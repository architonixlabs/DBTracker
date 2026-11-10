using DBTracker.Core.Enums;

namespace DBTracker.Core.Entities;

/// <summary>
/// One observed node's version within a clustered/replica-set instance (D01) — deliberately
/// separate from Epic 17's Region/Cluster/ReplicaSet topology facets on DatabaseInstance. Topology
/// says where a node lives; this says what version was observed running on it. A cluster can be
/// mid-upgrade with members on different builds — this models that explicitly instead of collapsing
/// the whole cluster into one version field.
/// </summary>
public class ClusterNodeVersion
{
    public int Id { get; set; }

    public int DatabaseInstanceId { get; set; }
    public DatabaseInstance? DatabaseInstance { get; set; }

    public int? VersionIdentityId { get; set; }
    public VersionIdentity? VersionIdentity { get; set; }

    public string NodeName { get; set; } = string.Empty;
    public string RawVersionText { get; set; } = string.Empty;

    public VersionResolutionStatus ResolutionStatus { get; set; } = VersionResolutionStatus.Unresolved;
    public string Family { get; set; } = string.Empty;
    public int? Major { get; set; }
    public int? Minor { get; set; }
    public string Build { get; set; } = string.Empty;

    public DateTime ObservedAtUtc { get; set; } = DateTime.UtcNow;
    public string Notes { get; set; } = string.Empty;
}
