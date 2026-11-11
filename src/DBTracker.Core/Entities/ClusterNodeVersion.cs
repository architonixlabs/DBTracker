using DBTracker.Core.Enums;

namespace DBTracker.Core.Entities;

/// <summary>The version observed on one node of a cluster or replica set, so a mid-upgrade cluster isn't collapsed into one version.</summary>
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
