using DBTracker.Core.Enums;

namespace DBTracker.Core.Entities;

/// <summary>A manually mapped dependency an instance relies on, or a downstream consumer. Unresolved rows flag a shared-engine change; two rows with different VersionState model a mixed-version transition.</summary>
public class InstanceDependency
{
    public int Id { get; set; }

    public int DatabaseInstanceId { get; set; }
    public DatabaseInstance? DatabaseInstance { get; set; }

    public DependencyKind Kind { get; set; }
    public DependencyDirection Direction { get; set; } = DependencyDirection.Upstream;

    public string Name { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;

    public int? OwnerTeamId { get; set; }
    public Team? OwnerTeam { get; set; }

    public string RecordedVersionText { get; set; } = string.Empty;
    public DependencyVersionState VersionState { get; set; } = DependencyVersionState.Unknown;

    public string OsRuntimeConstraint { get; set; } = string.Empty;
    public string ManagedProviderConstraint { get; set; } = string.Empty;

    public string EvidenceUrl { get; set; } = string.Empty;
    public string EvidenceNotes { get; set; } = string.Empty;

    /// <summary>Whether this dependency has been reviewed/confirmed compatible with a pending change. False (the default) is what flags it before a shared-engine change.</summary>
    public bool IsResolved { get; set; }
    public string ResolutionNotes { get; set; } = string.Empty;

    public DateTime? LastReviewedAtUtc { get; set; }
    public string LastReviewedBy { get; set; } = string.Empty;

    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAtUtc { get; set; } = DateTime.UtcNow;
    public string UpdatedBy { get; set; } = string.Empty;
}
