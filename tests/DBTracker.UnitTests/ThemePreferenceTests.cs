using DBTracker.Core.Services;
using Xunit;

namespace DBTracker.UnitTests;

public class ThemePreferenceTests
{
    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("sepia")]
    [InlineData("<script>")]
    public void MissingOrUnknown_IsTheDarkDefault(string? stored)
    {
        Assert.Equal(ThemePreference.Dark, ThemePreference.Normalize(stored));
        Assert.Equal(ThemePreference.Default, ThemePreference.Dark);
    }

    [Theory]
    [InlineData("system", "system")]
    [InlineData("dark", "dark")]
    [InlineData("light", "light")]
    [InlineData(" Light ", "light")]
    [InlineData("SYSTEM", "system")]
    public void KnownValues_AreKept(string stored, string expected)
    {
        Assert.Equal(expected, ThemePreference.Normalize(stored));
    }

    [Fact]
    public void ThreeChoices_InDisplayOrder()
    {
        Assert.Equal(["system", "dark", "light"], ThemePreference.All);
    }
}
