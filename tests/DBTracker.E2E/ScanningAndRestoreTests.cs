using Microsoft.Playwright;

namespace DBTracker.E2E;

[TestFixture]
public class ScanningAndRestoreTests
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
    public async Task ConfiguringSharedDriveLocation_AndScanning_DiscoversNewBackupFile()
    {
        // A real folder on disk, standing in for a shared drive — the server runs on the same
        // machine as this test, so a local temp folder is a faithful stand-in for a UNC path.
        var shareDir = Path.Combine(Path.GetTempPath(), $"dbtracker-e2e-share-{Guid.NewGuid():N}");
        Directory.CreateDirectory(shareDir);
        try
        {
            await File.WriteAllTextAsync(Path.Combine(shareDir, "full-2026-11-09.bak"), "fake backup contents");

            // Create a fresh instance to scan, so this test doesn't depend on seeded data's state.
            var instanceName = $"E2E-SCAN-{Guid.NewGuid():N}".Substring(0, 18).ToUpperInvariant();
            await GotoAsync("/instances/new");
            await _page.WaitForSelectorAsync("button[type=submit]");
            await WaitForCircuitAsync();
            await FillByLabel("Instance name", instanceName);
            await FillByLabel("Application", "E2E Scan Target");
            await FillByLabel("Backup file location", shareDir);
            await _page.ClickAsync("button[type=submit]:text('Save')");
            await _page.WaitForURLAsync(url => url.Contains("/instances/"));
            await WaitForCircuitAsync();

            await Assertions.Expect(_page.Locator("dd", new PageLocatorOptions { HasText = shareDir })).ToBeVisibleAsync();

            await _page.ClickAsync("button:has-text('Scan now')");
            await _page.WaitForTimeoutAsync(1500);

            await Assertions.Expect(_page.Locator("table.table-sm tbody tr")).ToHaveCountAsync(1);
            await Assertions.Expect(_page.Locator("table.table-sm tbody tr").First).ToContainTextAsync("Discovered");

            // Last-scanned status should now read OK, not "Never scanned".
            await Assertions.Expect(_page.Locator("dd", new PageLocatorOptions { HasText = "Never scanned" })).Not.ToBeVisibleAsync();
        }
        finally
        {
            Directory.Delete(shareDir, recursive: true);
        }
    }

    [Test]
    public async Task LoggingARestore_ShowsInHistory_AndFlagsAsTestedOnCompliance()
    {
        var instanceName = $"E2E-RESTORE-{Guid.NewGuid():N}".Substring(0, 20).ToUpperInvariant();

        await GotoAsync("/instances/new");
        await _page.WaitForSelectorAsync("button[type=submit]");
        await WaitForCircuitAsync();
        await FillByLabel("Instance name", instanceName);
        await _page.ClickAsync("button[type=submit]:text('Save')");
        await _page.WaitForURLAsync(url => url.Contains("/instances/"));
        await WaitForCircuitAsync();

        // No restore logged yet.
        await Assertions.Expect(_page.Locator("text=No restore ever logged")).ToBeVisibleAsync();

        await FillByLabel("Performed by", "Priya Nair");
        await SelectByLabel("Reason", "DrDrill");
        await SelectByLabel("Outcome", "Success");
        await _page.ClickAsync("button[type=submit]:text('Log restore')");
        await _page.WaitForTimeoutAsync(800);

        await Assertions.Expect(_page.Locator("table.table-sm", new PageLocatorOptions { HasText = "Priya Nair" })).ToBeVisibleAsync();

        await GotoAsync("/compliance");
        await _page.WaitForSelectorAsync(".dbt-table table");
        await WaitForCircuitAsync();

        var row = _page.Locator(".dbt-table tbody tr", new PageLocatorOptions { HasText = instanceName });
        await Assertions.Expect(row).ToContainTextAsync("Yes");
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
        _page.GotoAsync(TestServerFixture.BaseUrl + path, new PageGotoOptions { WaitUntil = WaitUntilState.Load });

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
