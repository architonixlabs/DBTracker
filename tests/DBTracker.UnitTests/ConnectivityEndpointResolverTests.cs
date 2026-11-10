using DBTracker.Core.Enums;
using DBTracker.Core.Services;
using Xunit;

namespace DBTracker.UnitTests;

public class ConnectivityEndpointResolverTests
{
    [Fact]
    public void NoHostAtAll_IsDisabled()
    {
        var result = ConnectivityEndpointResolver.Resolve("", "", DatabaseEngine.SqlServer, null, null);

        Assert.False(result.CanCheck);
        Assert.NotNull(result.DisabledReason);
    }

    [Fact]
    public void ServerIpPreferredOverDatabaseIp_WhenNoOverride()
    {
        var result = ConnectivityEndpointResolver.Resolve("10.0.0.1", "10.0.0.2", DatabaseEngine.SqlServer, null, null);

        Assert.Equal("10.0.0.1", result.Host);
        Assert.False(result.UsedHostOverride);
    }

    [Fact]
    public void FallsBackToDatabaseIp_WhenServerIpEmpty()
    {
        var result = ConnectivityEndpointResolver.Resolve("", "10.0.0.2", DatabaseEngine.SqlServer, null, null);

        Assert.Equal("10.0.0.2", result.Host);
    }

    [Fact]
    public void HostOverride_TakesPrecedenceOverServerAndDatabaseIp()
    {
        var result = ConnectivityEndpointResolver.Resolve("10.0.0.1", "10.0.0.2", DatabaseEngine.SqlServer, "db.internal.contoso.com", null);

        Assert.Equal("db.internal.contoso.com", result.Host);
        Assert.True(result.UsedHostOverride);
    }

    [Fact]
    public void NoPortOverride_UsesEngineDefault()
    {
        var result = ConnectivityEndpointResolver.Resolve("10.0.0.1", "", DatabaseEngine.PostgreSql, null, null);

        Assert.True(result.CanCheck);
        Assert.Equal(5432, result.Port);
        Assert.False(result.UsedPortOverride);
    }

    [Fact]
    public void ValidPortOverride_IsUsed()
    {
        var result = ConnectivityEndpointResolver.Resolve("10.0.0.1", "", DatabaseEngine.PostgreSql, null, 55432);

        Assert.Equal(55432, result.Port);
        Assert.True(result.UsedPortOverride);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    [InlineData(70000)]
    public void InvalidPortOverride_FallsBackToEngineDefault_RatherThanBeingUsedAsIs(int badPort)
    {
        var result = ConnectivityEndpointResolver.Resolve("10.0.0.1", "", DatabaseEngine.PostgreSql, null, badPort);

        Assert.True(result.CanCheck);
        Assert.Equal(5432, result.Port);
        Assert.False(result.UsedPortOverride);
    }

    [Fact]
    public void EngineWithNoDefaultPort_AndNoOverride_IsDisabled()
    {
        var result = ConnectivityEndpointResolver.Resolve("10.0.0.1", "", DatabaseEngine.Other, null, null);

        Assert.False(result.CanCheck);
        Assert.NotNull(result.DisabledReason);
    }

    [Fact]
    public void EngineWithNoDefaultPort_ButValidOverride_CanCheck()
    {
        var result = ConnectivityEndpointResolver.Resolve("10.0.0.1", "", DatabaseEngine.Other, null, 12345);

        Assert.True(result.CanCheck);
        Assert.Equal(12345, result.Port);
    }
}
