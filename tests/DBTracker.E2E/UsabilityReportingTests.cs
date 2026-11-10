using Microsoft.Playwright;

namespace DBTracker.E2E;

[TestFixture]
public class UsabilityReportingTests
{
    private IPage _page = null!;

    [SetUp]
    public async Task SetUp()
    {
        _page = await GlobalSetup.Server.Browser.NewPageAsync();
        await LoginAsync("priya.nair@architonixlabs.com");
    }

    [TearDown]
    public async Task TearDown()
    {
        await _page.CloseAsync();
    }

    [Test]
    public async Task BackupActivity_CustomRange_RendersWithoutError()
    {
        await GotoAsync("/reports");
        await _page.WaitForSelectorAsync(".stat-strip");

        await _page.ClickAsync("button:has-text('Custom')");
        await WaitForCircuitAsync();

        // Custom date inputs appear, and picking a window still renders the stat strip cleanly.
        await Assertions.Expect(_page.Locator("input[type=date]")).ToHaveCountAsync(2);
        await Assertions.Expect(_page.Locator(".stat-strip")).ToBeVisibleAsync();
    }

    [Test]
    public async Task LoggingABackup_WithBackdatedOccurrenceTime_RecordsEntryTimeSeparately_AndCapturesRecorder()
    {
        var instanceName = $"E2E-BACKDATE-{Guid.NewGuid():N}".Substring(0, 20).ToUpperInvariant();

        await GotoAsync("/instances/new");
        await _page.WaitForSelectorAsync("button[type=submit]");
        await WaitForCircuitAsync();
        await FillByLabel("Instance name", instanceName);
        await _page.ClickAsync("button[type=submit]:text('Save')");
        await _page.WaitForURLAsync(url => url.Contains("/instances/"));
        await WaitForCircuitAsync();

        // Backdate the occurrence time to yesterday — simulating logging a backup late, after an incident.
        var yesterday = DateTime.Now.AddDays(-1).ToString("yyyy-MM-ddTHH:mm");
        await _page.Locator("input[type=datetime-local]").First.FillAsync(yesterday);
        await SelectByLabel("Status", "Success");
        await _page.ClickAsync("button[type=submit]:text('Log backup')");
        await _page.WaitForTimeoutAsync(800);

        var row = _page.Locator("table.dbt-table tbody tr").First;
        await Assertions.Expect(row).ToContainTextAsync("priya.nair@architonixlabs.com");

        // The occurrence time (backdated) and entry time (now) should differ by roughly a day,
        // not be forced to the same "now" value.
        var cells = await row.Locator("td").AllTextContentsAsync();
        Assert.That(cells[0], Is.Not.EqualTo(cells[1]));
    }

    [Test]
    public async Task CorrectingABackupEntry_PreservesOriginal_AndShowsCorrectedMarker()
    {
        var instanceName = $"E2E-CORRECT-{Guid.NewGuid():N}".Substring(0, 19).ToUpperInvariant();

        await GotoAsync("/instances/new");
        await _page.WaitForSelectorAsync("button[type=submit]");
        await WaitForCircuitAsync();
        await FillByLabel("Instance name", instanceName);
        await _page.ClickAsync("button[type=submit]:text('Save')");
        await _page.WaitForURLAsync(url => url.Contains("/instances/"));
        await WaitForCircuitAsync();

        await SelectByLabel("Status", "Success");
        await _page.ClickAsync("button[type=submit]:text('Log backup')");
        await _page.WaitForTimeoutAsync(800);

        // ".mt-4" scopes to the "Correct a backup entry" panel, which comes before the restore one in DOM order.
        await _page.Locator(".mt-4 select").First.SelectOptionAsync(new SelectOptionValue { Index = 1 });
        await WaitForCircuitAsync();

        await SelectByLabel("New status", "Failed");
        await FillByLabel("Reason (required)", "Scan tool mis-flagged a partial write as a full success.");
        await _page.ClickAsync("button:has-text('Save correction')");
        await _page.WaitForTimeoutAsync(800);

        var row = _page.Locator("table.dbt-table tbody tr").First;
        await Assertions.Expect(row).ToContainTextAsync("Failed");
        await Assertions.Expect(row).ToContainTextAsync("corrected");
    }

    [Test]
    public async Task ComplianceSnapshot_CanBeSaved_AndReopenedWithFrozenValues()
    {
        // "Save snapshot" is gated to Roles.ReportingPolicy (Admin/ITManager/Auditor) — Priya (DBA, this
        // fixture's default login) can't see it, so switch to an Auditor account for this test.
        await LoginAsync("angela.price@contoso.com");

        await GotoAsync("/compliance");
        await _page.WaitForSelectorAsync(".dbt-table table");
        await WaitForCircuitAsync();

        await _page.ClickAsync("button:has-text('Save snapshot')");
        await _page.WaitForTimeoutAsync(800);

        await Assertions.Expect(_page.Locator(".dbt-toast")).ToContainTextAsync("Snapshot saved");

        await _page.Locator("a:has-text('View')").First.ClickAsync();
        await _page.WaitForURLAsync(url => url.Contains("/compliance/snapshots/"));
        await WaitForCircuitAsync();

        await Assertions.Expect(_page.Locator("text=frozen as of generation time")).ToBeVisibleAsync();
        await Assertions.Expect(_page.Locator(".dbt-table table tbody tr").First).ToBeVisibleAsync();
    }

    [Test]
    public async Task RestoreTestingReport_DistinguishesNeverTested_FromExpiredCoverage()
    {
        await GotoAsync("/reports");
        await WaitForCircuitAsync();
        await _page.ClickAsync("button:text-is('Restore testing')");
        await _page.WaitForSelectorAsync(".dbt-table table");

        await Assertions.Expect(_page.Locator("text=expired restore-test coverage")).ToBeVisibleAsync();
        await Assertions.Expect(_page.Locator("th:has-text('Coverage')")).ToBeVisibleAsync();
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

    private Task GotoAsync(string path) =>
        _page.GotoAsync(TestServerFixture.BaseUrl + path, new PageGotoOptions { WaitUntil = WaitUntilState.NetworkIdle });

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
