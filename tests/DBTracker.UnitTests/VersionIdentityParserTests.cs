using DBTracker.Core.Enums;
using DBTracker.Core.Services;
using Xunit;

namespace DBTracker.UnitTests;

public class VersionIdentityParserTests
{
    [Fact]
    public void EmptyString_IsUnresolved()
    {
        var result = VersionIdentityParser.Parse(DatabaseEngine.PostgreSql, "");
        Assert.Equal(VersionResolutionStatus.Unresolved, result.Status);
        Assert.NotEmpty(result.UnresolvedReason);
    }

    [Fact]
    public void GarbledText_IsUnresolved()
    {
        var result = VersionIdentityParser.Parse(DatabaseEngine.PostgreSql, "not-a-version-at-all");
        Assert.Equal(VersionResolutionStatus.Unresolved, result.Status);
    }

    [Fact]
    public void OtherEngine_AlwaysUnresolved()
    {
        var result = VersionIdentityParser.Parse(DatabaseEngine.Other, "16.2");
        Assert.Equal(VersionResolutionStatus.Unresolved, result.Status);
        Assert.Contains("Other/custom", result.UnresolvedReason);
    }

    [Theory]
    [InlineData("16.2", 16, 2)]
    [InlineData("13.14 (Debian 13.14-1)", 13, 14)]
    [InlineData("15", 15, null)]
    public void PostgreSql_ResolvesMajorMinor(string raw, int expectedMajor, int? expectedMinor)
    {
        var result = VersionIdentityParser.Parse(DatabaseEngine.PostgreSql, raw);
        Assert.Equal(VersionResolutionStatus.Resolved, result.Status);
        Assert.Equal("PostgreSQL", result.Family);
        Assert.Equal(expectedMajor, result.Major);
        Assert.Equal(expectedMinor, result.Minor);
    }

    [Fact]
    public void PostgreSql_DetectsKnownDistribution()
    {
        var result = VersionIdentityParser.Parse(DatabaseEngine.PostgreSql, "13.14 (Debian 13.14-1)");
        Assert.Equal("Debian", result.Distribution);
    }

    [Theory]
    [InlineData("2022", 2022)]
    [InlineData("2019 CU18", 2019)]
    public void SqlServer_MarketingYearForm_Resolves(string raw, int expectedYear)
    {
        var result = VersionIdentityParser.Parse(DatabaseEngine.SqlServer, raw);
        Assert.Equal(VersionResolutionStatus.Resolved, result.Status);
        Assert.Equal("SQL Server", result.Family);
        Assert.Equal(expectedYear, result.Major);
    }

    [Fact]
    public void SqlServer_PatchTrain_Captured()
    {
        var result = VersionIdentityParser.Parse(DatabaseEngine.SqlServer, "2019 CU18");
        Assert.Equal("CU18", result.PatchTrain);
    }

    [Fact]
    public void SqlServer_InternalBuildForm_MapsToYear()
    {
        var result = VersionIdentityParser.Parse(DatabaseEngine.SqlServer, "16.0.2000.5");
        Assert.Equal(VersionResolutionStatus.Resolved, result.Status);
        Assert.Equal(2022, result.Major);
        Assert.Equal("2000.5", result.Build);
    }

    [Fact]
    public void SqlServer_UnknownInternalMajor_IsUnresolved()
    {
        var result = VersionIdentityParser.Parse(DatabaseEngine.SqlServer, "99.0.1.1");
        Assert.Equal(VersionResolutionStatus.Unresolved, result.Status);
    }

    [Fact]
    public void MySql_StandardBuild_Resolves()
    {
        var result = VersionIdentityParser.Parse(DatabaseEngine.MySql, "8.0.36");
        Assert.Equal(VersionResolutionStatus.Resolved, result.Status);
        Assert.Equal("MySQL", result.Family);
        Assert.Equal(8, result.Major);
        Assert.Equal(0, result.Minor);
    }

    [Fact]
    public void MySql_MariaDbLabel_IsNeedsReview_NotSilentlyTreatedAsMySql()
    {
        var result = VersionIdentityParser.Parse(DatabaseEngine.MySql, "10.6.16-MariaDB");
        Assert.Equal(VersionResolutionStatus.NeedsReview, result.Status);
        Assert.Equal("MariaDB", result.Family);
        Assert.NotEmpty(result.UnresolvedReason);
    }

    [Fact]
    public void MongoDb_StandardForm_Resolves()
    {
        var result = VersionIdentityParser.Parse(DatabaseEngine.MongoDb, "7.0.5");
        Assert.Equal(VersionResolutionStatus.Resolved, result.Status);
        Assert.Equal("MongoDB", result.Family);
        Assert.Equal(7, result.Major);
        Assert.Equal(0, result.Minor);
        Assert.Equal("5", result.Build);
    }

    [Theory]
    [InlineData("19c", 19)]
    [InlineData("21c", 21)]
    public void Oracle_CloudNamingForm_Resolves(string raw, int expectedMajor)
    {
        var result = VersionIdentityParser.Parse(DatabaseEngine.Oracle, raw);
        Assert.Equal(VersionResolutionStatus.Resolved, result.Status);
        Assert.Equal("Oracle Database", result.Family);
        Assert.Equal(expectedMajor, result.Major);
    }

    [Fact]
    public void Oracle_LegacyDottedForm_Resolves()
    {
        var result = VersionIdentityParser.Parse(DatabaseEngine.Oracle, "11.2.0.4");
        Assert.Equal(VersionResolutionStatus.Resolved, result.Status);
        Assert.Equal(11, result.Major);
        Assert.Equal(2, result.Minor);
        Assert.Equal("0.4", result.Build);
    }

    [Fact]
    public void Oracle_UnrecognizedForm_IsUnresolved()
    {
        var result = VersionIdentityParser.Parse(DatabaseEngine.Oracle, "some-custom-fork");
        Assert.Equal(VersionResolutionStatus.Unresolved, result.Status);
    }

    [Fact]
    public void NullRawText_IsUnresolved()
    {
        var result = VersionIdentityParser.Parse(DatabaseEngine.MongoDb, null);
        Assert.Equal(VersionResolutionStatus.Unresolved, result.Status);
    }
}
