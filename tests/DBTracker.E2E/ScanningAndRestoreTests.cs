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

    /// <summary>G07: a file rewritten under the same name and size is new evidence, an unchanged file is not re-recorded,
    /// and a .trn log backup is recorded as a log backup rather than a full one.</summary>
    [Test]
    public async Task RewrittenSameSizeFile_IsNewEvidence_AndLogBackupsAreTypedAsLogs()
    {
        var shareDir = Path.Combine(Path.GetTempPath(), $"dbtracker-e2e-rewrite-{Guid.NewGuid():N}");
        Directory.CreateDirectory(shareDir);
        try
        {
            var nightly = Path.Combine(shareDir, "nightly.bak");
            await File.WriteAllTextAsync(nightly, "night one contents");
            File.SetLastWriteTimeUtc(nightly, DateTime.UtcNow.AddHours(-26));
            var log = Path.Combine(shareDir, "nightly_0215.trn");
            await File.WriteAllTextAsync(log, "log backup bytes");
            File.SetLastWriteTimeUtc(log, DateTime.UtcNow.AddHours(-25));

            var instanceName = $"E2E-REWRITE-{Guid.NewGuid():N}".Substring(0, 18).ToUpperInvariant();
            await GotoAsync("/instances/new");
            await _page.WaitForSelectorAsync("button[type=submit]");
            await WaitForCircuitAsync();
            await FillByLabel("Instance name", instanceName);
            await FillByLabel("Backup file location", shareDir);
            await _page.ClickAsync("button[type=submit]:text('Save')");
            await _page.WaitForURLAsync(url => url.Contains("/instances/"));
            await WaitForCircuitAsync();

            await _page.ClickAsync("button:has-text('Scan now')");
            await _page.WaitForTimeoutAsync(1500);
            var historyRows = _page.Locator("table.table-sm tbody tr");
            await Assertions.Expect(historyRows).ToHaveCountAsync(2);
            await Assertions.Expect(historyRows.Filter(new LocatorFilterOptions { HasText = "nightly_0215.trn" })).ToContainTextAsync("TransactionLog");

            // Scanning again with nothing changed records nothing new.
            await _page.ClickAsync("button:has-text('Scan now')");
            await _page.WaitForTimeoutAsync(1500);
            await Assertions.Expect(historyRows).ToHaveCountAsync(2);

            // The next night's job overwrites the file: same name, same size, new write time.
            await File.WriteAllTextAsync(nightly, "night two contents");
            File.SetLastWriteTimeUtc(nightly, DateTime.UtcNow.AddHours(-2));
            await _page.ClickAsync("button:has-text('Scan now')");
            await _page.WaitForTimeoutAsync(1500);
            await Assertions.Expect(historyRows).ToHaveCountAsync(3);
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
        await _page.FillAsync("input[autocomplete~=username]", email);
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
