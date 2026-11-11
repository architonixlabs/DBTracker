using System.Text;
using System.Text.Json;
using DBTracker.Core.Entities;
using DBTracker.Core.Enums;
using DBTracker.Infrastructure;
using Microsoft.EntityFrameworkCore;
using Microsoft.Playwright;

namespace DBTracker.E2E;

/// <summary>G28: lab baseline of Core Web Vitals (LCP, CLS) and interaction latency on the main pages, desktop and an
/// emulated phone (360 px, 4x CPU slowdown), over a representative inventory. Explicit: run on purpose, it adds 1,000
/// instances to the test database (recreated at the start of every E2E run).</summary>
/// <remarks>Collected with PerformanceObserver (largest-contentful-paint, layout-shift, event with durationThreshold 16,
/// long-animation-frame) in an init script; the measuring context bypasses the CSP only so the observer can be injected.
/// Interaction latency is the longest event duration per interaction, the lab stand-in for INP.</remarks>
[TestFixture, Explicit("Benchmark: run on purpose with --filter CoreWebVitalsBaseline")]
public class CoreWebVitalsBaseline
{
    private const string TestConnectionString =
        "Server=localhost;Database=DBTracker_Test;Integrated Security=True;TrustServerCertificate=True;";

    private const string ObserverScript = """
        window.__cwv = { lcp: 0, cls: 0, events: [], loafs: [] };
        new PerformanceObserver(l => { for (const e of l.getEntries()) window.__cwv.lcp = e.startTime; })
          .observe({ type: 'largest-contentful-paint', buffered: true });
        new PerformanceObserver(l => { for (const e of l.getEntries()) if (!e.hadRecentInput) window.__cwv.cls += e.value; })
          .observe({ type: 'layout-shift', buffered: true });
        new PerformanceObserver(l => { for (const e of l.getEntries()) if (e.interactionId) window.__cwv.events.push({ name: e.name, duration: e.duration, id: e.interactionId,
            inputDelay: e.processingStart - e.startTime, processing: e.processingEnd - e.processingStart, presentation: e.startTime + e.duration - e.processingEnd }); })
          .observe({ type: 'event', durationThreshold: 16, buffered: true });
        try { new PerformanceObserver(l => { for (const e of l.getEntries()) window.__cwv.loafs.push(e.duration); })
          .observe({ type: 'long-animation-frame', buffered: true }); } catch {}
        """;

    private sealed record PageCheck(string Name, string Path, string ReadySelector, string? ClickSelector);

    [Test]
    public async Task MeasureMainPages_DesktopAndPhone()
    {
        await EnsureRepresentativeInventoryAsync();
        var instanceId = await FirstInstanceIdAsync();
        var pages = new[]
        {
            new PageCheck("Dashboard", "/", ".stat-strip", "button:has-text('Refresh')"),
            new PageCheck("Instances", "/instances", ".dbt-table tbody tr", "th button:has-text('Name')"),
            new PageCheck("Reports", "/reports", ".stat-strip", "button:text-is('Restore testing')"),
            new PageCheck("Instance detail", $"/instances/{instanceId}", "h1", "button:has-text('Test connection')"),
            new PageCheck("New instance form", "/instances/new", "button[type=submit]:text('Save instance')", null),
        };

        var report = new StringBuilder("| Profile | Page | Load to ready ms | LCP ms | CLS | Slowest interaction ms (input / processing / presentation) | Long frames > 50 ms |\n|---|---|---|---|---|---|---|\n");
        foreach (var (profile, mobile) in new[] { ("Desktop", false), ("Phone 360px, 4x CPU", true) })
        {
            var context = await GlobalSetup.Server.Browser.NewContextAsync(new BrowserNewContextOptions
            {
                BypassCSP = true,
                ViewportSize = mobile ? new ViewportSize { Width = 360, Height = 800 } : new ViewportSize { Width = 1366, Height = 900 },
                IsMobile = mobile,
                HasTouch = mobile,
            });
            await context.AddInitScriptAsync(ObserverScript);
            var page = await context.NewPageAsync();
            if (mobile)
            {
                var cdp = await context.NewCDPSessionAsync(page);
                await cdp.SendAsync("Emulation.setCPUThrottlingRate", new Dictionary<string, object> { ["rate"] = 4 });
            }

            await LoginAsync(page);
            foreach (var check in pages)
            {
                // Two loads: the first warms server caches and the circuit; the second is the one recorded.
                double loadMs = 0;
                for (var run = 0; run < 2; run++)
                {
                    var started = System.Diagnostics.Stopwatch.StartNew();
                    await page.GotoAsync(TestServerFixture.BaseUrl + check.Path, new PageGotoOptions { WaitUntil = WaitUntilState.Load, Timeout = 180000 });
                    await page.WaitForSelectorAsync(check.ReadySelector, new PageWaitForSelectorOptions { Timeout = 180000 });
                    loadMs = started.Elapsed.TotalMilliseconds;
                    await page.WaitForTimeoutAsync(1500);
                }

                if (check.ClickSelector is { } click && await page.Locator(click).CountAsync() > 0)
                {
                    await page.Locator(click).First.ClickAsync();
                    await page.WaitForTimeoutAsync(1500);
                }

                var json = await page.EvaluateAsync<JsonElement>("() => window.__cwv");
                report.Append(Row(profile, check.Name, loadMs, json));
            }

            await context.CloseAsync();
        }

        var output = report.ToString();
        TestContext.Out.WriteLine(output);
        var outPath = Path.Combine(TestContext.CurrentContext.WorkDirectory, "core-web-vitals-baseline.md");
        await File.WriteAllTextAsync(outPath, output);
        TestContext.Out.WriteLine("Written to " + outPath);
    }

    private static string Row(string profile, string page, double loadMs, JsonElement cwv)
    {
        var lcp = cwv.GetProperty("lcp").GetDouble();
        var cls = cwv.GetProperty("cls").GetDouble();
        var slowest = cwv.GetProperty("events").EnumerateArray()
            .OrderByDescending(e => e.GetProperty("duration").GetDouble())
            .Select(e => $"{e.GetProperty("duration").GetDouble():N0} ({e.GetProperty("inputDelay").GetDouble():N0} / {e.GetProperty("processing").GetDouble():N0} / {e.GetProperty("presentation").GetDouble():N0}) {e.GetProperty("name").GetString()}")
            .FirstOrDefault() ?? "none over 16 ms";
        var longFrames = cwv.GetProperty("loafs").EnumerateArray().Count(d => d.GetDouble() > 50);
        return $"| {profile} | {page} | {loadMs:N0} | {lcp:N0} | {cls:N3} | {slowest} | {longFrames} |\n";
    }

    private static async Task LoginAsync(IPage page)
    {
        await page.GotoAsync(TestServerFixture.BaseUrl + "/Account/Login", new PageGotoOptions { WaitUntil = WaitUntilState.Load });
        await page.FillAsync("input[autocomplete~=username]", "priya.nair@architonixlabs.com");
        await page.FillAsync("input[autocomplete=current-password]", "ArxDemo#2026!");
        await page.ClickAsync("button[type=submit]");
        await page.WaitForURLAsync(url => !url.Contains("/Account/Login"), new PageWaitForURLOptions { Timeout = 15000 });
    }

    private static DBTrackerDbContext Db() =>
        new(new DbContextOptionsBuilder<DBTrackerDbContext>().UseSqlServer(TestConnectionString).Options);

    private static async Task<int> FirstInstanceIdAsync()
    {
        using var db = Db();
        return await db.DatabaseInstances.Where(i => !i.IsArchived).OrderBy(i => i.Id).Select(i => i.Id).FirstAsync();
    }

    /// <summary>Tops the inventory up to 1,000 instances with 30 days of daily backups each, matching the U06 benchmark's scale.</summary>
    private static async Task EnsureRepresentativeInventoryAsync()
    {
        using var db = Db();
        var existing = await db.DatabaseInstances.CountAsync();
        if (existing >= 1000)
        {
            return;
        }

        var engines = Enum.GetValues<DatabaseEngine>();
        var now = DateTime.UtcNow;
        var random = new Random(42);
        for (var batch = existing; batch < 1000; batch += 100)
        {
            for (var i = batch; i < Math.Min(batch + 100, 1000); i++)
            {
                var instance = new DatabaseInstance
                {
                    Name = $"CWV-{i:D4}",
                    Engine = engines[i % engines.Length],
                    Application = $"App {i % 40}",
                    Company = $"Company {i % 8}",
                    Department = $"Dept {i % 12}",
                    Environment = (EnvironmentTier)(i % 5),
                    ExpectedBackupIntervalHours = 24,
                    CreatedAtUtc = now.AddDays(-60),
                    UpdatedAtUtc = now.AddDays(-60),
                };
                for (var d = 0; d < 30; d++)
                {
                    instance.BackupRecords.Add(new BackupRecord
                    {
                        TimestampUtc = now.AddDays(-d).AddMinutes(-random.Next(0, 600)),
                        Type = BackupType.Full,
                        Status = random.Next(0, 20) == 0 ? BackupStatus.Failed : BackupStatus.Success,
                        SizeMb = random.Next(100, 50000),
                        DurationMinutes = random.Next(1, 120),
                        Source = BackupSource.Manual,
                    });
                }
                db.DatabaseInstances.Add(instance);
            }
            await db.SaveChangesAsync();
            db.ChangeTracker.Clear();
        }
    }
}
