using DBTracker.Infrastructure;
using Microsoft.EntityFrameworkCore;

namespace DBTracker.E2E;

[SetUpFixture]
public class GlobalSetup
{
    public static TestServerFixture Server { get; private set; } = null!;

    private const string TestConnectionString =
        "Server=localhost;Database=DBTracker_Test;Integrated Security=True;TrustServerCertificate=True;";

    [OneTimeSetUp]
    public async Task RunBeforeAnyTests()
    {
        var options = new DbContextOptionsBuilder<DBTrackerDbContext>()
            .UseSqlServer(TestConnectionString)
            .Options;

        using (var db = new DBTrackerDbContext(options))
        {
            await db.Database.EnsureDeletedAsync();
        }

        Server = new TestServerFixture();
        await Server.InitializeAsync();
    }

    [OneTimeTearDown]
    public async Task RunAfterAllTests()
    {
        await Server.DisposeAsync();
    }
}
