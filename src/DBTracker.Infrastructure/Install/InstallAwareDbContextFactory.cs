using Microsoft.EntityFrameworkCore;

namespace DBTracker.Infrastructure.Install;

/// <summary>
/// Singleton factory that stamps each new context with the current connection string, so finishing the wizard repoints the app
/// without a restart. It stays a singleton on purpose: options built in a request scope capture that scope's service provider,
/// and contexts created after the scope ends (a component timer outliving its page) would hit a disposed provider.
/// </summary>
public sealed class InstallAwareDbContextFactory(DbContextOptions<DBTrackerDbContext> options, InstallState state) : IDbContextFactory<DBTrackerDbContext>
{
    public DBTrackerDbContext CreateDbContext()
    {
        var db = new DBTrackerDbContext(options);
        var connectionString = state.ConnectionString;
        if (!string.IsNullOrWhiteSpace(connectionString))
        {
            db.Database.SetConnectionString(connectionString);
        }
        return db;
    }
}
