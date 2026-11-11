using Microsoft.Playwright;

namespace DBTracker.E2E;

/// <summary>E2E coverage for Epic 13 (U01-U04): evidence levels, the health explanation panel, scan rule preview, and manual refresh.</summary>
[TestFixture]
public class EvidenceAndHealthExplanationTests
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
    public async Task DiscoveredBackup_ShowsDiscoveredEvidenceBadge_AndFileName()
    {
        var shareDir = Path.Combine(Path.GetTempPath(), $"dbtracker-e2e-evidence-{Guid.NewGuid():N}");
        Directory.CreateDirectory(shareDir);
        try
        {
            await File.WriteAllTextAsync(Path.Combine(shareDir, "full-2026-11-10.bak"), "fake backup contents");

            var instanceName = $"E2E-EVID-{Guid.NewGuid():N}".Substring(0, 18).ToUpperInvariant();
            await GotoAsync("/instances/new");
            await WaitForCircuitAsync();
            await FillByLabel("Instance name", instanceName);
            await FillByLabel("Backup file location", shareDir);
            await _page.ClickAsync("button[type=submit]:text('Save')");
            await _page.WaitForURLAsync(url => url.Contains("/instances/"));
            await WaitForCircuitAsync();

            await _page.ClickAsync("button:has-text('Scan now')");
            await _page.WaitForTimeoutAsync(1500);

            var row = _page.Locator("table.table-sm tbody tr").First;
            await Assertions.Expect(row).ToContainTextAsync("Discovered");
            await Assertions.Expect(row).ToContainTextAsync("full-2026-11-10.bak");
        }
        finally
        {
            Directory.Delete(shareDir, recursive: true);
        }
    }

    [Test]
    public async Task SuccessfulRestoreAfterBackup_PromotesEvidenceToRestoreTested()
    {
        var instanceName = $"E2E-RTEST-{Guid.NewGuid():N}".Substring(0, 18).ToUpperInvariant();

        await GotoAsync("/instances/new");
        await WaitForCircuitAsync();
        await FillByLabel("Instance name", instanceName);
        await _page.ClickAsync("button[type=submit]:text('Save')");
        await _page.WaitForURLAsync(url => url.Contains("/instances/"));
        await WaitForCircuitAsync();

        // Log a manual backup — evidence should be "Reported success" until a restore proves it.
        await _page.ClickAsync("button[type=submit]:text('Log backup')");
        await _page.WaitForTimeoutAsync(800);

        var backupRow = _page.Locator("table.table-sm tbody tr").First;
        await Assertions.Expect(backupRow).ToContainTextAsync("Reported success");

        // Now log a successful restore — the same backup row should promote to Restore-tested.
        await FillByLabel("Performed by", "Priya Nair");
        await SelectByLabel("Outcome", "Success");
        await _page.ClickAsync("button[type=submit]:text('Log restore')");
        await _page.WaitForTimeoutAsync(800);

        await Assertions.Expect(backupRow).ToContainTextAsync("Restore-tested");
    }

    [Test]
    public async Task HealthExplanationPanel_ShowsGoverningIntervalAndNextDue()
    {
        var instanceName = $"E2E-EXPLAIN-{Guid.NewGuid():N}".Substring(0, 18).ToUpperInvariant();

        await GotoAsync("/instances/new");
        await WaitForCircuitAsync();
        await FillByLabel("Instance name", instanceName);
        await _page.ClickAsync("button[type=submit]:text('Save')");
        await _page.WaitForURLAsync(url => url.Contains("/instances/"));
        await WaitForCircuitAsync();

        var explanationCard = _page.Locator(".dbt-card", new PageLocatorOptions { HasText = "Health explanation" });
        await Assertions.Expect(explanationCard).ToBeVisibleAsync();
        await Assertions.Expect(explanationCard).ToContainTextAsync("every 24 h");

        // Never backed up yet, so there's no next-due date and no alert suppression.
        await Assertions.Expect(explanationCard).ToContainTextAsync("No backup evidence recorded yet");
        await Assertions.Expect(explanationCard).ToContainTextAsync("Not suppressed");

        // Logging a backup now should populate "Last successful backup" and "Next due".
        await _page.ClickAsync("button[type=submit]:text('Log backup')");
        await _page.WaitForTimeoutAsync(800);

        await Assertions.Expect(explanationCard.Locator("dt:text-is('Next due') ~ dd, dt:text-is('Next due') + dd").First).Not.ToContainTextAsync("No successful backup yet");
    }

    [Test]
    public async Task ScanPreview_DistinguishesEligibleFromExcludedFiles()
    {
        var shareDir = Path.Combine(Path.GetTempPath(), $"dbtracker-e2e-preview-{Guid.NewGuid():N}");
        Directory.CreateDirectory(shareDir);
        try
        {
            await File.WriteAllTextAsync(Path.Combine(shareDir, "full.bak"), "some backup bytes");
            await File.WriteAllTextAsync(Path.Combine(shareDir, "empty.bak"), "");
            await File.WriteAllTextAsync(Path.Combine(shareDir, "readme.txt"), "not a backup");

            var instanceName = $"E2E-PREVIEW-{Guid.NewGuid():N}".Substring(0, 18).ToUpperInvariant();
            await GotoAsync("/instances/new");
            await WaitForCircuitAsync();
            await FillByLabel("Instance name", instanceName);
            await FillByLabel("Backup file location", shareDir);
            // Blur, since the pattern field only renders once BackupFileLocation's change event
            // (InputText binds on blur/change here, not keystroke-by-keystroke) reaches the server.
            await _page.Keyboard.PressAsync("Tab");
            await FillByLabel("Backup file name pattern(s)", "*.bak");

            await _page.ClickAsync("button:has-text('Preview scan')");
            await _page.WaitForTimeoutAsync(800);

            var previewTable = _page.Locator(".dbt-card", new PageLocatorOptions { HasText = "Scan preview" }).Locator("table");
            await Assertions.Expect(previewTable.Locator("tr", new LocatorLocatorOptions { HasText = "full.bak" })).ToContainTextAsync("Would be recorded");
            await Assertions.Expect(previewTable.Locator("tr", new LocatorLocatorOptions { HasText = "empty.bak" })).ToContainTextAsync("Excluded");
            await Assertions.Expect(previewTable.Locator("tr", new LocatorLocatorOptions { HasText = "readme.txt" })).ToContainTextAsync("Excluded");
        }
        finally
        {
            Directory.Delete(shareDir, recursive: true);
        }
    }

    [Test]
    public async Task DashboardManualRefresh_ShowsUpdatedTimestampAndAutoRefreshToggle()
    {
        await GotoAsync("/");
        await _page.WaitForSelectorAsync(".stat-strip");
        await WaitForCircuitAsync();

        await Assertions.Expect(_page.Locator("text=Updated")).ToBeVisibleAsync();

        await _page.ClickAsync("button:has-text('Auto-refresh on')");
        await Assertions.Expect(_page.Locator("button:has-text('Auto-refresh paused')")).ToBeVisibleAsync();

        await _page.ClickAsync("button:has-text('Refresh')");
        await _page.WaitForTimeoutAsync(500);
        await Assertions.Expect(_page.Locator(".stat-strip")).ToBeVisibleAsync();
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
