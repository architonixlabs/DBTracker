using DBTracker.Core.Entities;
using DBTracker.Core.Enums;

namespace DBTracker.Core.Services;

/// <summary>Pure summary of one instance's recorded technical dependencies (D07), as of right now — no persistence, no discovery, just arithmetic over what's already been manually recorded.</summary>
public record DependencyImpactResult(
    int TotalCount,
    int UnresolvedCount,
    int DownstreamConsumerCount,
    IReadOnlyList<InstanceDependency> UnresolvedDependencies,
    IReadOnlyList<InstanceDependency> DownstreamConsumers,
    bool HasUnresolvedConcerns);

/// <summary>
/// Computes the "before you change a shared engine" view D07 asks for: downstream consumers and
/// unresolved dependencies, both shown explicitly rather than folded into one score. A dependency
/// that's never been reviewed (IsResolved == false, the default on a freshly recorded row) always
/// counts as unresolved — nothing here infers resolution from silence.
/// </summary>
public static class DependencyImpactAssessor
{
    public static DependencyImpactResult Assess(IReadOnlyList<InstanceDependency> dependencies)
    {
        var unresolved = dependencies.Where(d => !d.IsResolved).ToList();
        var downstream = dependencies.Where(d => d.Direction == DependencyDirection.Downstream).ToList();

        return new DependencyImpactResult(
            dependencies.Count,
            unresolved.Count,
            downstream.Count,
            unresolved,
            downstream,
            unresolved.Count > 0);
    }
}
