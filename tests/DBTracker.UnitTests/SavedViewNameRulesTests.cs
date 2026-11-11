using DBTracker.Core.Services;
using Xunit;

namespace DBTracker.UnitTests;

public class SavedViewNameRulesTests
{
    [Theory]
    [InlineData("  Prod  DBs ", "Prod DBs")]
    [InlineData("My\tinstances", "My instances")]
    [InlineData(null, "")]
    public void Normalize_TrimsAndCollapsesWhitespace(string? input, string expected) =>
        Assert.Equal(expected, SavedViewNameRules.Normalize(input));

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData(null)]
    public void Validate_RejectsBlank(string? name) =>
        Assert.Equal("Enter a name for the view.", SavedViewNameRules.Validate(name));

    [Fact]
    public void Validate_RejectsOverColumnLength()
    {
        Assert.NotNull(SavedViewNameRules.Validate(new string('a', SavedViewNameRules.MaxLength + 1)));
        Assert.Null(SavedViewNameRules.Validate(new string('a', SavedViewNameRules.MaxLength)));
    }
}
