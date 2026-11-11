using Microsoft.Playwright;

namespace DBTracker.E2E;

/// <summary>
/// E2E coverage for Epic 17's U22: a two-period trend comparison that always shows the inventory
/// denominator, flags a period with no Compliance snapshot evidence rather than silently inferring
/// it from today's live inventory, and lets the user drill from a trend number into the specific
/// rows behind it.
/// </summary>
[TestFixture]
public class Epic17TrendsTests
{
    private IPage _page = null!;

    [SetUp]
    public async Task SetUp()
    {
        _page = await GlobalSetup.Server.Browser.NewPageAsync();
        // Roles.ReportingPolicy (Save snapshot) is Admin/ITManager/Auditor, not DBA — use Admin.
        await LoginAsync("admin@architonixlabs.com");
    }

    [TearDown]
    public async Task TearDown() => await _page.CloseAsync();

    [Test]
    public async Task TrendComparison_FlagsMissingEvidenceForAnOlderPeriod_AndShowsRealDataForTheCurrentOne()
    {
        // Establish real Compliance-snapshot evidence covering "now" (Period B's default range
        // ends today). Period A's default range ends ~30 days before this snapshot even exists,
        // so it must come back as missing evidence rather than a silently inferred number.
        await GotoAsync("/compliance");
        await _page.WaitForSelectorAsync(".dbt-table table");
        await WaitForCircuitAsync();
        await _page.ClickAsync("button:has-text('Save snapshot')");
        await _page.WaitForTimeoutAsync(800);
        await Assertions.Expect(_page.Locator(".dbt-toast").First).ToContainTextAsync("Snapshot saved");

        await GotoAsync("/reports?type=Trends");
        await _page.WaitForSelectorAsync("#trend-a-from");
        await WaitForCircuitAsync();

        await _page.ClickAsync("button:has-text('Compare periods')");
        await _page.WaitForTimeoutAsync(800);

        // Period A: no snapshot existed yet at its end date — flagged explicitly, not interpolated.
        var missingEvidenceAlert = _page.Locator(".alert-danger", new PageLocatorOptions { HasText = "Period A has no Compliance snapshot" });
        await Assertions.Expect(missingEvidenceAlert).ToBeVisibleAsync();

        var comparisonTable = _page.Locator("[role=region][aria-label*='Trend comparison']");
        await Assertions.Expect(comparisonTable).ToContainTextAsync("No snapshot: missing evidence");

        // Period B: the inventory denominator is a real number (every seeded instance), not blank/zero-by-default.
        var inventoryRow = comparisonTable.Locator("tr", new LocatorLocatorOptions { HasText = "Inventory (denominator)" });
        await Assertions.Expect(inventoryRow).ToBeVisibleAsync();
        var inventoryCells = await inventoryRow.Locator("td").AllTextContentsAsync();
        Assert.That(inventoryCells.Last().Trim(), Is.Not.Empty.And.Not.EqualTo("No data"));
        Assert.That(int.Parse(inventoryCells.Last().Trim()), Is.GreaterThanOrEqualTo(10));

        // Drill-down: Period B's at-risk rows table lists the actual seeded Failed/Overdue
        // instances behind the "at-risk" number, not just the count.
        await Assertions.Expect(_page.Locator("text=Period B: at-risk rows")).ToBeVisibleAsync();
        var periodBDrillDown = _page.Locator("[role=region][aria-label='Period B at-risk instances']");
        await Assertions.Expect(periodBDrillDown).ToBeVisibleAsync();
        await Assertions.Expect(periodBDrillDown).ToContainTextAsync("CONTOSO-HR-MYSQL");
    }

    [Test]
    public async Task TrendsTab_StatesRetentionDaysAreNotProofFilesStillExistOnDisk()
    {
        await GotoAsync("/reports?type=Trends");
        await WaitForCircuitAsync();

        await Assertions.Expect(_page.Locator("text=Retention-days values alone are not evidence")).ToBeVisibleAsync();
    }

    private Task LoginAsync(string email) => LoginAsync(email, "ArxDemo#2026!");

    private async Task LoginAsync(string email, string password)
    {
        await GotoAsync("/Account/Login");
        await _page.FillAsync("input[autocomplete~=username]", email);
        await _page.FillAsync("input[autocomplete=current-password]", password);
        await _page.ClickAsync("button[type=submit]");
        await _page.WaitForURLAsync(url => !url.Contains("/Account/Login"), new PageWaitForURLOptions { Timeout = 15000 });
        await WaitForCircuitAsync();
    }

    private Task GotoAsync(string path) =>
        _page.GotoAsync(TestServerFixture.BaseUrl + path, new PageGotoOptions { WaitUntil = WaitUntilState.Load });

    private Task WaitForCircuitAsync() => _page.WaitForTimeoutAsync(800);
}
