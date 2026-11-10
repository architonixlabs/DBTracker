using DBTracker.Core.Services;
using Xunit;

namespace DBTracker.UnitTests;

public class ReportScopeTests
{
    [Fact]
    public void Full_IncludesAnyInstanceId()
    {
        var scope = ReportScope.Full();

        Assert.True(scope.Includes(1));
        Assert.True(scope.Includes(999));
        Assert.True(scope.IsFullInventory);
    }

    [Fact]
    public void Filtered_IncludesOnlyListedIds()
    {
        var scope = ReportScope.Filtered([1, 3, 5], "Production only");

        Assert.True(scope.Includes(1));
        Assert.True(scope.Includes(3));
        Assert.False(scope.Includes(2));
        Assert.False(scope.IsFullInventory);
        Assert.Equal("Production only", scope.Description);
    }

    [Fact]
    public void Filtered_WithNoIds_IncludesNothing()
    {
        var scope = ReportScope.Filtered([], "Empty");

        Assert.False(scope.Includes(1));
    }
}
