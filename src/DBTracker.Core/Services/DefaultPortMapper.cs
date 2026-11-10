using DBTracker.Core.Enums;

namespace DBTracker.Core.Services;

/// <summary>Conventional default TCP port per engine — a starting guess for the connectivity check, not a guarantee (real deployments remap ports).</summary>
public static class DefaultPortMapper
{
    public static int GetDefaultPort(DatabaseEngine engine) => engine switch
    {
        DatabaseEngine.SqlServer => 1433,
        DatabaseEngine.MySql => 3306,
        DatabaseEngine.PostgreSql => 5432,
        DatabaseEngine.MongoDb => 27017,
        DatabaseEngine.Oracle => 1521,
        _ => 0
    };
}
