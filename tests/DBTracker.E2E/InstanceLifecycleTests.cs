using Microsoft.Playwright;

namespace DBTracker.E2E;

[TestFixture]
public class InstanceLifecycleTests
{
    private IPage _page = null!;

    [SetUp]
    public async Task SetUp()
    {
        _page = await GlobalSetup.Server.Browser.NewPageAsync();
        await LoginAsync("priya.nair@architonixlabs.com");
    }

    private async Task LoginAsync(string email)
    {
        await GotoAsync("/Account/Login");
        await _page.FillAsync("input[autocomplete=username]", email);
        await _page.FillAsync("input[autocomplete=current-password]", "ArxDemo#2026!");

        // Click-then-wait races if the form navigation completes before WaitForURLAsync starts
        // listening; wrapping both in WaitForNavigationAsync avoids that.
        await _page.RunAndWaitForNavigationAsync(
            () => _page.ClickAsync("button[type=submit]"),
            new PageRunAndWaitForNavigationOptions { WaitUntil = WaitUntilState.NetworkIdle, Timeout = 15000 });

        await WaitForCircuitAsync();
    }

    [TearDown]
    public async Task TearDown()
    {
        await _page.CloseAsync();
    }

    [Test]
    public async Task Dashboard_ShowsSeededInstanceCount()
    {
        await GotoAsync("/");
        await _page.WaitForSelectorAsync("text=Dashboard");

        // >= rather than == 10: other tests in this run may have created additional instances
        // against the same shared Testing database (reset once per suite, not per test).
        var totalText = await _page.Locator(".stat-cell.Total .stat-value").InnerTextAsync();
        Assert.That(int.Parse(totalText), Is.GreaterThanOrEqualTo(10));
    }

    [Test]
    public async Task CreateInstance_LogBackup_HealthBadgeBecomesOnTrack()
    {
        var instanceName = $"E2E-TEST-{Guid.NewGuid():N}".Substring(0, 20).ToUpperInvariant();

        // Create a new instance.
        await GotoAsync("/instances/new");
        await _page.WaitForSelectorAsync("button[type=submit]");
        await WaitForCircuitAsync();
        await FillByLabel("Instance name", instanceName);

        await SelectByLabel("Engine", "PostgreSql");
        await FillByLabel("Application", "E2E Test App");
        await FillByLabel("Company", "Architonix Labs");
        await FillByLabel("Department", "QA");
        await FillByLabel("Contact name", "Test Runner");
        await FillByLabel("Contact email", "qa@architonixlabs.com");
        await FillByLabel("Database IP", "10.0.0.50");
        await FillByLabel("Server IP", "10.0.0.50");

        await _page.ClickAsync("button[type=submit]:text('Save')");

        // Should land on the detail page, with Never backed up health.
        await _page.WaitForURLAsync(url => url.Contains("/instances/"));
        await Assertions.Expect(_page.Locator("h1")).ToContainTextAsync(instanceName);
        await Assertions.Expect(_page.Locator(".badge-health")).ToContainTextAsync("Never backed up");

        // Log a successful backup.
        await SelectByLabel("Status", "Success");
        await _page.ClickAsync("button[type=submit]:text('Log backup')");

        // Health badge should now read On track.
        await Assertions.Expect(_page.Locator(".badge-health").First).ToContainTextAsync("On track");

        // History table should show one Success row.
        await Assertions.Expect(_page.Locator("table.table-sm tbody tr")).ToHaveCountAsync(1);
    }

    [Test]
    public async Task InstancesList_SearchFilter_NarrowsResults()
    {
        await GotoAsync("/instances");
        await _page.WaitForSelectorAsync(".dbt-table table");
        await WaitForCircuitAsync();

        var rowsBefore = await _page.Locator(".dbt-table tbody tr").CountAsync();
        Assert.That(rowsBefore, Is.GreaterThan(1));

        await _page.FillAsync("input[placeholder*='Search']", "Contoso");
        await _page.WaitForTimeoutAsync(600);

        var rows = _page.Locator(".dbt-table tbody tr");
        var count = await rows.CountAsync();
        Assert.That(count, Is.GreaterThan(0));

        for (var i = 0; i < count; i++)
        {
            var text = await rows.Nth(i).InnerTextAsync();
            Assert.That(text, Does.Contain("Contoso"));
        }
    }

    [Test]
    public async Task InstancesList_EngineFilter_ShowsOnlyMatchingEngine()
    {
        await GotoAsync("/instances");
        await _page.WaitForSelectorAsync(".dbt-table table");
        await WaitForCircuitAsync();

        await _page.Locator(".filter-bar select").Nth(0).SelectOptionAsync(new SelectOptionValue { Label = "MongoDb" });
        await _page.WaitForTimeoutAsync(600);

        var rows = _page.Locator(".dbt-table tbody tr");
        var count = await rows.CountAsync();
        Assert.That(count, Is.GreaterThan(0));

        for (var i = 0; i < count; i++)
        {
            var text = await rows.Nth(i).InnerTextAsync();
            Assert.That(text, Does.Contain("MongoDb"));
        }
    }

    [Test]
    public async Task DashboardStatCard_FiltersTableByHealth()
    {
        await GotoAsync("/");
        await _page.WaitForSelectorAsync(".stat-cell.Failed");
        await WaitForCircuitAsync();

        await _page.ClickAsync(".stat-cell.Failed");
        await _page.WaitForTimeoutAsync(600);

        var badges = _page.Locator(".dbt-table tbody .badge-health");
        var count = await badges.CountAsync();
        Assert.That(count, Is.GreaterThan(0));

        for (var i = 0; i < count; i++)
        {
            var text = await badges.Nth(i).InnerTextAsync();
            Assert.That(text, Is.EqualTo("Failed"));
        }
    }

    [Test]
    public async Task Compliance_ExportCsv_ProducesDownload()
    {
        await GotoAsync("/compliance");
        await _page.WaitForSelectorAsync(".dbt-table table");
        await WaitForCircuitAsync();

        var downloadTask = _page.WaitForDownloadAsync();
        await _page.ClickAsync("button:text('Export CSV')");
        var download = await downloadTask;

        Assert.That(download.SuggestedFilename, Does.StartWith("dbtracker-compliance-"));
        Assert.That(download.SuggestedFilename, Does.EndWith(".csv"));
    }

    [Test]
    public async Task ViewerRole_CannotSeeAddInstanceButton_AndIsBlockedFromEditorRoute()
    {
        await LoginAsync("dev.patel@ediqra.com");

        await GotoAsync("/instances");
        await _page.WaitForSelectorAsync(".dbt-table table");
        Assert.That(await _page.Locator("a:has-text('Add instance')").CountAsync(), Is.EqualTo(0));

        await GotoAsync("/instances/new");
        await _page.WaitForURLAsync(url => url.Contains("/Account/AccessDenied"));
    }

    private Task GotoAsync(string path) =>
        _page.GotoAsync(TestServerFixture.BaseUrl + path, new PageGotoOptions { WaitUntil = WaitUntilState.NetworkIdle });

    /// <summary>
    /// Blazor Server static-prerenders a page, then its interactive SignalR circuit attaches a
    /// moment later. Interacting before that attach completes loses the event entirely. Waiting for
    /// network-idle navigation gets most of the way there; this adds a short settle buffer on top.
    /// </summary>
    private Task WaitForCircuitAsync() => _page.WaitForTimeoutAsync(800);

    private async Task FillByLabel(string label, string value)
    {
        await _page.Locator($"label:text-is('{label}') ~ input, label:text-is('{label}') + input").First.FillAsync(value);
    }

    private async Task SelectByLabel(string label, string optionText)
    {
        var select = _page.Locator($"label:text-is('{label}') ~ select, label:text-is('{label}') + select").First;
        await select.SelectOptionAsync(new SelectOptionValue { Label = optionText });
    }
}
