using DBTracker.Core.Services;
using Xunit;

namespace DBTracker.UnitTests;

public class VersionBuildComparerTests
{
    [Theory]
    [InlineData("9", "10", -1)]
    [InlineData("10", "9", 1)]
    [InlineData("2000.5", "4000.1", -1)]
    [InlineData("16.2", "16.2", 0)]
    public void Compare_NumericSegments_OrderNumerically(string a, string b, int expectedSign)
    {
        var result = VersionBuildComparer.Compare(a, b);
        Assert.Equal(expectedSign, Math.Sign(result));
    }

    [Fact]
    public void Compare_NullOrEmpty_TreatedAsLesser()
    {
        Assert.True(VersionBuildComparer.Compare(null, "1") < 0);
        Assert.True(VersionBuildComparer.Compare("1", null) > 0);
        Assert.Equal(0, VersionBuildComparer.Compare(null, null));
    }

    [Theory]
    [InlineData("16.2", "16.0", "16.4", true)]
    [InlineData("16.5", "16.0", "16.4", false)]
    [InlineData("15.9", "16.0", "16.4", false)]
    public void IsInRange_InclusiveBounds(string value, string min, string max, bool expected)
    {
        Assert.Equal(expected, VersionBuildComparer.IsInRange(value, min, max));
    }

    [Fact]
    public void IsInRange_NoMinMeansAnyBelowMax()
    {
        Assert.True(VersionBuildComparer.IsInRange("1.0", null, "16.4"));
    }

    [Fact]
    public void IsInRange_EmptyValue_NeverInRange()
    {
        Assert.False(VersionBuildComparer.IsInRange("", "1", "2"));
    }
}
