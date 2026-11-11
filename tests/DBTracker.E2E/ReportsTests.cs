using Microsoft.Playwright;

namespace DBTracker.E2E;

[TestFixture]
public class ReportsTests
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

        await _page.ClickAsync("button[type=submit]");
        await _page.WaitForURLAsync(url => !url.Contains("/Account/Login"), new PageWaitForURLOptions { Timeout = 15000 });

        await WaitForCircuitAsync();
    }

    [TearDown]
    public async Task TearDown()
    {
        await _page.CloseAsync();
    }

    [Test]
    public async Task ReportsNav_OpensBackupActivityByDefault_WithRangeAndTable()
    {
        await GotoAsync("/reports");
        await _page.WaitForSelectorAsync("text=Backup activity");

        // Default tab is Backup activity, defaulting to the 7-day range.
        Assert.That(await _page.Locator(".stat-strip").CountAsync(), Is.EqualTo(1));
        Assert.That(await _page.Locator(".dbt-table table").CountAsync(), Is.GreaterThanOrEqualTo(1));
    }

    [Test]
    public async Task SwitchingReportTypes_RendersEachReport()
    {
        await GotoAsync("/reports");
        await WaitForCircuitAsync();

        await _page.ClickAsync("button:text-is('Restore testing')");
        await _page.WaitForSelectorAsync("text=successful restore proven");

        await _page.ClickAsync("button:text-is('Ownership roll-up')");
        await _page.WaitForSelectorAsync("text=Primary contact");

        await _page.ClickAsync("button:text-is('Scan activity')");
        await _page.WaitForSelectorAsync("text=Shared-drive scan status");

        await _page.ClickAsync("button:text-is('Compliance snapshot')");
        await _page.WaitForSelectorAsync("a:has-text('Open Compliance snapshot')");

        await _page.ClickAsync("button:text-is('Management summary')");
        await _page.WaitForSelectorAsync("pre");
        var body = await _page.Locator("pre").InnerTextAsync();
        Assert.That(body, Does.Contain("DBTracker backup compliance report"));
    }

    [Test]
    public async Task BackupActivity_ExportCsv_ProducesDownload()
    {
        await GotoAsync("/reports");
        await _page.WaitForSelectorAsync(".dbt-table table");

        var downloadTask = _page.WaitForDownloadAsync();
        await _page.ClickAsync("button:text('Export CSV')");
        var download = await downloadTask;

        Assert.That(download.SuggestedFilename, Does.StartWith("dbtracker-backup-activity-"));
        Assert.That(download.SuggestedFilename, Does.EndWith(".csv"));
    }

    [Test]
    public async Task OwnershipRollup_ExportCsv_ProducesDownload()
    {
        await GotoAsync("/reports");
        await WaitForCircuitAsync();
        await _page.ClickAsync("button:text-is('Ownership roll-up')");
        await _page.WaitForSelectorAsync(".dbt-table table");

        var downloadTask = _page.WaitForDownloadAsync();
        await _page.ClickAsync("button:text('Export CSV')");
        var download = await downloadTask;

        Assert.That(download.SuggestedFilename, Does.StartWith("dbtracker-ownership-rollup-"));
    }

    [Test]
    public async Task ManagementSummary_SendNow_VisibleToReportingRoles_NotToDba()
    {
        // Priya (DBA) can view the Management summary body but not send it — sending is gated
        // to Admin/ITManager/Auditor (leadership-facing), same policy as the Compliance page.
        await GotoAsync("/reports");
        await WaitForCircuitAsync();
        await _page.ClickAsync("button:text-is('Management summary')");
        await _page.WaitForSelectorAsync("pre");
        Assert.That(await _page.Locator("button:has-text('Send now')").CountAsync(), Is.EqualTo(0));

        await LoginAsync("marcus.chen@architonixlabs.com");
        await GotoAsync("/reports");
        await WaitForCircuitAsync();
        await _page.ClickAsync("button:text-is('Management summary')");
        await _page.WaitForSelectorAsync("button:has-text('Send now')");
    }

    [Test]
    public async Task BackupActivity_RangeButtons_ChangeSelectedWindow()
    {
        await GotoAsync("/reports");
        await _page.WaitForSelectorAsync(".stat-strip");

        await _page.ClickAsync("button:has-text('Last 90 d')");
        await WaitForCircuitAsync();

        // Switching range shouldn't error out, and the stat strip should still render.
        Assert.That(await _page.Locator(".stat-strip").CountAsync(), Is.EqualTo(1));
    }

    private Task GotoAsync(string path) =>
        _page.GotoAsync(TestServerFixture.BaseUrl + path, new PageGotoOptions { WaitUntil = WaitUntilState.Load });

    private Task WaitForCircuitAsync() => _page.WaitForTimeoutAsync(800);
}
