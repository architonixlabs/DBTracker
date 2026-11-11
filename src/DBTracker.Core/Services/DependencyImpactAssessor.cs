using DBTracker.Core.Entities;
using DBTracker.Core.Enums;

namespace DBTracker.Core.Services;

/// <summary>Summary of one instance's recorded dependencies.</summary>
public record DependencyImpactResult(
    int TotalCount,
    int UnresolvedCount,
    int DownstreamConsumerCount,
    IReadOnlyList<InstanceDependency> UnresolvedDependencies,
    IReadOnlyList<InstanceDependency> DownstreamConsumers,
    bool HasUnresolvedConcerns);

/// <summary>Downstream consumers and unresolved dependencies to review before changing a shared engine, listed separately rather than scored. An unreviewed row always counts as unresolved.</summary>
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
