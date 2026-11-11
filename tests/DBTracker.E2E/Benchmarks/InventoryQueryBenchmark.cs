using System.Diagnostics;
using DBTracker.Core.Entities;
using DBTracker.Core.Enums;
using DBTracker.Core.Services;
using DBTracker.Infrastructure;
using DBTracker.Infrastructure.Queries;
using Microsoft.EntityFrameworkCore;

// Deliberately outside the DBTracker.E2E namespace: that namespace's [SetUpFixture] drops the shared
// E2E database and starts the web server on port 5299, which this benchmark must never touch.
namespace DBTracker.Benchmarks;

/// <summary>U06 baseline: 1,000 instances and 100,000 backup records (the fixture suggested in USABILITY_REVIEW.md),
/// in its own database. Records timings for the overview queries and checks the SQL-side Instances paging returns
/// exactly what the in-memory reference filter returns.</summary>
/// <remarks>Explicit, so a normal test run skips it. Run with:
/// dotnet test tests/DBTracker.E2E --filter "FullyQualifiedName~InventoryQueryBenchmark"
/// Uses LocalDB unless DBTRACKER_BENCH_CONNECTION names another server. Timings land in TestContext output and in
/// bench-results.txt beside the test assembly. The database is dropped and recreated on every run.</remarks>
[TestFixture, Explicit("Benchmark: seeds 100,000 rows into its own database"), Category("Benchmark")]
public class InventoryQueryBenchmark
{
    private const int InstanceCount = 1_000;
    private const int BackupsPerInstance = 109; // 923 instances with backups x 109 = 100,607 records
    private const int RestoresPerInstance = 4;
    private const int Repeats = 5;

    private static string ConnectionString =>
        Environment.GetEnvironmentVariable("DBTRACKER_BENCH_CONNECTION")
        ?? @"Server=(localdb)\MSSQLLocalDB;Database=DBTracker_Bench_U06;Integrated Security=True;TrustServerCertificate=True;";

    private sealed class Factory(DbContextOptions<DBTrackerDbContext> options) : IDbContextFactory<DBTrackerDbContext>
    {
        public DBTrackerDbContext CreateDbContext() => new(options);
    }

    private Factory _factory = null!;
    private InstanceOverviewService _service = null!;
    private readonly List<string> _lines = new();

    [OneTimeSetUp]
    public async Task SeedAsync()
    {
        var options = new DbContextOptionsBuilder<DBTrackerDbContext>().UseSqlServer(ConnectionString).Options;
        _factory = new Factory(options);
        _service = new InstanceOverviewService(_factory);

        await using (var db = _factory.CreateDbContext())
        {
            await db.Database.EnsureDeletedAsync();
            await db.Database.EnsureCreatedAsync();
        }

        var seed = Stopwatch.StartNew();
        var now = DateTime.UtcNow;
        var rng = new Random(20261111);
        var engines = Enum.GetValues<DatabaseEngine>();
        var environments = Enum.GetValues<EnvironmentTier>();

        for (var batch = 0; batch < InstanceCount / 100; batch++)
        {
            await using var db = _factory.CreateDbContext();
            db.ChangeTracker.AutoDetectChangesEnabled = false;
            for (var n = 0; n < 100; n++)
            {
                var idx = batch * 100 + n;
                var instance = new DatabaseInstance
                {
                    Name = $"BENCH-{idx:0000}",
                    Engine = engines[idx % engines.Length],
                    Environment = environments[idx % environments.Length],
                    Role = idx % 5 == 0 ? InstanceRole.Passive : InstanceRole.Active,
                    Application = $"App {idx % 120}",
                    Company = $"Company {idx % 8}",
                    Department = $"Dept {idx % 25}",
                    ContactName = $"Owner {idx % 60}",
                    ContactEmail = $"owner{idx % 60}@example.test",
                    Tags = idx % 3 == 0 ? "prod, critical" : "batch;reporting",
                    ExpectedBackupIntervalHours = 24,
                    BackupFileLocation = idx % 2 == 0 ? $@"\\bench\share\{idx}" : "",
                };

                // 109 backups an instance, newest roughly an hour to three days old, so health spreads across states.
                var newestAgeHours = rng.Next(1, 72);
                for (var b = 0; b < BackupsPerInstance; b++)
                {
                    var failed = b == 0 && idx % 11 == 0;
                    instance.BackupRecords.Add(new BackupRecord
                    {
                        TimestampUtc = now.AddHours(-(newestAgeHours + b * 24)),
                        Status = failed ? BackupStatus.Failed : BackupStatus.Success,
                        Source = b % 4 == 0 ? BackupSource.Discovered : BackupSource.Manual,
                        SizeMb = 500 + rng.Next(0, 5000),
                        DurationMinutes = 5 + rng.Next(0, 60),
                    });
                }
                if (idx % 13 == 0)
                {
                    instance.BackupRecords.Clear();
                }
                for (var r = 0; r < RestoresPerInstance; r++)
                {
                    instance.RestoreEvents.Add(new RestoreEvent
                    {
                        TimestampUtc = now.AddDays(-(r * 30 + rng.Next(0, 20))),
                        Outcome = r == 1 ? RestoreOutcome.Failed : RestoreOutcome.Success,
                    });
                }
                db.DatabaseInstances.Add(instance);
            }
            await db.SaveChangesAsync();
        }

        await using (var db = _factory.CreateDbContext())
        {
            Log($"Seeded {await db.DatabaseInstances.CountAsync()} instances, {await db.BackupRecords.CountAsync()} backup records, " +
                $"{await db.RestoreEvents.CountAsync()} restore events in {seed.Elapsed.TotalSeconds:N1}s.");
        }
    }

    [OneTimeTearDown]
    public void WriteResults()
    {
        var path = Path.Combine(TestContext.CurrentContext.TestDirectory, "bench-results.txt");
        File.WriteAllLines(path, _lines);
        TestContext.Out.WriteLine($"Results written to {path}");
    }

    [Test, Order(1)]
    public async Task Timings()
    {
        var today = DateTime.UtcNow;
        await TimeAsync("GetAllAsync (old: every history)", () => _service.GetAllAsync());
        await TimeAsync("GetAllLatestAsync (Reports/Compliance)", () => _service.GetAllLatestAsync());
        await TimeAsync("GetAllSummariesAsync (dashboard)", () => _service.GetAllSummariesAsync());
        await TimeAsync("Old paging: summaries + in-memory page", async () => InstanceListFilter.Apply(await _service.GetAllSummariesAsync(), new InstanceListQuery()));
        await TimeAsync("GetPagedAsync page 1, name sort", () => _service.GetPagedAsync(new InstanceListQuery()));
        await TimeAsync("GetPagedAsync page 20, risk sort", () => _service.GetPagedAsync(new InstanceListQuery(Sort: InstanceSortKey.Risk, Page: 20)));
        await TimeAsync("GetPagedAsync search + health=Overdue", () => _service.GetPagedAsync(new InstanceListQuery(Search: "App 1", Health: BackupHealth.Overdue)));
        await TimeAsync("GetBackupActivityAsync 7 days", () => _service.GetBackupActivityAsync(today.AddDays(-7), today));
        await TimeAsync("GetBackupActivityAsync 90 days", () => _service.GetBackupActivityAsync(today.AddDays(-90), today));
    }

    private static IEnumerable<InstanceListQuery> EquivalenceQueries()
    {
        yield return new InstanceListQuery();
        yield return new InstanceListQuery(Page: 7, PageSize: 50);
        yield return new InstanceListQuery(Sort: InstanceSortKey.Risk, Page: 3);
        yield return new InstanceListQuery(Sort: InstanceSortKey.Risk, Descending: true, Page: 2);
        yield return new InstanceListQuery(Sort: InstanceSortKey.Engine, Page: 4);
        yield return new InstanceListQuery(Sort: InstanceSortKey.Environment, Descending: true);
        yield return new InstanceListQuery(Sort: InstanceSortKey.Company, Page: 5);
        yield return new InstanceListQuery(Health: BackupHealth.Failed);
        yield return new InstanceListQuery(Health: BackupHealth.NeverBackedUp);
        yield return new InstanceListQuery(Health: BackupHealth.Overdue, Page: 2);
        yield return new InstanceListQuery(Health: BackupHealth.OnTrack, Page: 3);
        yield return new InstanceListQuery(Search: "app 11");
        yield return new InstanceListQuery(Company: "company 3", Department: "Dept 3");
        yield return new InstanceListQuery(Tag: "critical", Page: 2);
        yield return new InstanceListQuery(Tag: "reporting");
        yield return new InstanceListQuery(Engine: DatabaseEngine.PostgreSql, Environment: EnvironmentTier.Production);
        yield return new InstanceListQuery(Owner: "Owner 7");
        yield return new InstanceListQuery(MyInstancesOnly: true, MyEmail: "owner5@example.test");
        yield return new InstanceListQuery(Page: 9999);
    }

    [Test, Order(2)]
    public async Task SqlPaging_MatchesInMemoryReference()
    {
        var all = await _service.GetAllSummariesAsync();
        foreach (var query in EquivalenceQueries())
        {
            var expected = InstanceListFilter.Apply(all, query);
            var actual = await _service.GetPagedAsync(query);

            Assert.That(actual.TotalCount, Is.EqualTo(expected.TotalCount), $"count for {query}");
            Assert.That(actual.Page, Is.EqualTo(expected.Page), $"page for {query}");
            Assert.That(actual.Items.Select(r => r.Id), Is.EqualTo(expected.Items.Select(r => r.Id)), $"rows for {query}");
            Assert.That(actual.Items.Select(r => r.Health), Is.EqualTo(expected.Items.Select(r => r.Health)), $"health for {query}");
        }
    }

    [Test, Order(3)]
    public async Task LatestOverviews_MatchFullHistoryOverviews()
    {
        var full = (await _service.GetAllAsync()).ToDictionary(o => o.Instance.Id);
        var latest = await _service.GetAllLatestAsync();
        var now = DateTime.UtcNow;

        Assert.That(latest, Has.Count.EqualTo(full.Count));
        foreach (var o in latest)
        {
            var f = full[o.Instance.Id];
            Assert.That(o.Health, Is.EqualTo(f.Health), o.Instance.Name);
            Assert.That(o.LastBackupUtc, Is.EqualTo(f.LastBackupUtc), o.Instance.Name);
            Assert.That(o.Instance.RestoreEvents.Count, Is.EqualTo(f.Instance.RestoreEvents.Count), o.Instance.Name);
            Assert.That(
                BackupEvidenceClassifier.ClassifyLatestSuccess(o.Instance.BackupRecords.ToList(), o.Instance.RestoreEvents.ToList(), now),
                Is.EqualTo(BackupEvidenceClassifier.ClassifyLatestSuccess(f.Instance.BackupRecords.ToList(), f.Instance.RestoreEvents.ToList(), now)),
                o.Instance.Name);
        }
    }

    private async Task TimeAsync<T>(string label, Func<Task<T>> run)
    {
        await run(); // warm-up: plan compilation and connection pool
        var times = new List<double>();
        for (var i = 0; i < Repeats; i++)
        {
            var sw = Stopwatch.StartNew();
            await run();
            times.Add(sw.Elapsed.TotalMilliseconds);
        }
        times.Sort();
        Log($"{label,-48} median {times[times.Count / 2],8:N0} ms   max {times[^1],8:N0} ms");
    }

    private void Log(string line)
    {
        _lines.Add(line);
        TestContext.Out.WriteLine(line);
    }
}
