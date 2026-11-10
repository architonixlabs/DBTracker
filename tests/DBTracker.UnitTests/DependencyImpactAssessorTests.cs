using DBTracker.Core.Entities;
using DBTracker.Core.Enums;
using DBTracker.Core.Services;
using Xunit;

namespace DBTracker.UnitTests;

public class DependencyImpactAssessorTests
{
    private static InstanceDependency Dep(DependencyDirection direction = DependencyDirection.Upstream, bool isResolved = false) => new()
    {
        Kind = DependencyKind.Driver,
        Direction = direction,
        Name = "Test dependency",
        IsResolved = isResolved
    };

    [Fact]
    public void NoDependencies_NoConcerns()
    {
        var result = DependencyImpactAssessor.Assess([]);

        Assert.Equal(0, result.TotalCount);
        Assert.Equal(0, result.UnresolvedCount);
        Assert.Equal(0, result.DownstreamConsumerCount);
        Assert.False(result.HasUnresolvedConcerns);
    }

    [Fact]
    public void UnresolvedDependency_FlagsConcern()
    {
        var result = DependencyImpactAssessor.Assess([Dep(isResolved: false)]);

        Assert.True(result.HasUnresolvedConcerns);
        Assert.Equal(1, result.UnresolvedCount);
        Assert.Single(result.UnresolvedDependencies);
    }

    [Fact]
    public void AllResolvedDependencies_NoConcern()
    {
        var result = DependencyImpactAssessor.Assess([Dep(isResolved: true), Dep(isResolved: true)]);

        Assert.False(result.HasUnresolvedConcerns);
        Assert.Equal(0, result.UnresolvedCount);
    }

    [Fact]
    public void DownstreamConsumers_AreCountedAndListedSeparatelyFromUnresolved()
    {
        var dependencies = new List<InstanceDependency>
        {
            Dep(DependencyDirection.Downstream, isResolved: true),
            Dep(DependencyDirection.Upstream, isResolved: true),
        };
        var result = DependencyImpactAssessor.Assess(dependencies);

        Assert.Equal(1, result.DownstreamConsumerCount);
        Assert.Single(result.DownstreamConsumers);
        Assert.Equal(DependencyDirection.Downstream, result.DownstreamConsumers[0].Direction);
        Assert.False(result.HasUnresolvedConcerns); // both resolved, so no concern despite a downstream consumer existing
    }

    [Fact]
    public void MixedResolvedAndUnresolved_OnlyUnresolvedCounted()
    {
        var dependencies = new List<InstanceDependency> { Dep(isResolved: true), Dep(isResolved: false), Dep(isResolved: false) };
        var result = DependencyImpactAssessor.Assess(dependencies);

        Assert.Equal(3, result.TotalCount);
        Assert.Equal(2, result.UnresolvedCount);
        Assert.True(result.HasUnresolvedConcerns);
    }

    [Fact]
    public void UnresolvedDownstreamConsumer_AppearsInBothLists()
    {
        var dependency = Dep(DependencyDirection.Downstream, isResolved: false);
        var result = DependencyImpactAssessor.Assess([dependency]);

        Assert.Contains(dependency, result.UnresolvedDependencies);
        Assert.Contains(dependency, result.DownstreamConsumers);
    }
}
