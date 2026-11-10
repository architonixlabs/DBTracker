using DBTracker.Core.Enums;
using DBTracker.Core.Services;
using Xunit;

namespace DBTracker.UnitTests;

public class DefaultPortMapperTests
{
    [Theory]
    [InlineData(DatabaseEngine.SqlServer, 1433)]
    [InlineData(DatabaseEngine.MySql, 3306)]
    [InlineData(DatabaseEngine.PostgreSql, 5432)]
    [InlineData(DatabaseEngine.MongoDb, 27017)]
    [InlineData(DatabaseEngine.Oracle, 1521)]
    public void KnownEngines_MapToConventionalPort(DatabaseEngine engine, int expectedPort)
    {
        Assert.Equal(expectedPort, DefaultPortMapper.GetDefaultPort(engine));
    }

    [Fact]
    public void OtherEngine_HasNoDefaultPort()
    {
        Assert.Equal(0, DefaultPortMapper.GetDefaultPort(DatabaseEngine.Other));
    }
}
