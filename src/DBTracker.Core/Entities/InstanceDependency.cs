using DBTracker.Core.Enums;

namespace DBTracker.Core.Entities;

/// <summary>
/// One technical dependency relationship on an instance (D07): what it relies on (driver,
/// extension/plugin, job, linked service, replica/HA group, backup tooling, OS/runtime, managed
/// provider) or what relies on it (Direction = Downstream, a consuming application/deployment).
/// Manual entry/mapping is explicitly sufficient for this release — there is no auto-discovery.
/// IsResolved distinguishes a dependency that's been reviewed/confirmed from one still open; an
/// unresolved row is what DependencyImpactAssessor surfaces as a flag before a shared-engine change.
/// Recording two rows for the same Name/Kind with different VersionState (e.g. Legacy + Transitioning)
/// is how a mixed-version transition state is represented — nothing here forces one row per name.
/// </summary>
public class InstanceDependency
{
    public int Id { get; set; }

    public int DatabaseInstanceId { get; set; }
    public DatabaseInstance? DatabaseInstance { get; set; }

    public DependencyKind Kind { get; set; }
    public DependencyDirection Direction { get; set; } = DependencyDirection.Upstream;

    public string Name { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;

    /// <summary>Owner team (reuses Epic 17's Team concept per D07's acceptance criteria — not a new ownership model).</summary>
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
