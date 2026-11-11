using Microsoft.Playwright;

namespace DBTracker.E2E;

/// <summary>
/// E2E coverage for Epic 21 (D16-D20): DBA recovery and operational assurance. Priya (DBA) has
/// Roles.CanEditPolicy, which covers the three new per-instance pages this epic adds (Recovery,
/// Assurance, Certificates). Every test here checks the "stays honestly Unknown/not falsely green"
/// framing the backlog calls out — never that a feature merely renders.
/// </summary>
[TestFixture]
public class Epic21RecoveryAssuranceTests
{
    private IPage _page = null!;

    [SetUp]
    public async Task SetUp()
    {
        _page = await GlobalSetup.Server.Browser.NewPageAsync();
        await LoginAsync("priya.nair@architonixlabs.com");
    }

    [TearDown]
    public async Task TearDown() => await _page.CloseAsync();

    private async Task GoToInstanceSubPageAsync(string instanceSearchText, string suffix)
    {
        await GotoAsync($"/instances?s={instanceSearchText}");
        await _page.WaitForSelectorAsync(".dbt-table tbody tr");
        await WaitForCircuitAsync();
        await _page.Locator(".instance-row-link").First.ClickAsync();
        await _page.WaitForURLAsync(url => url.Contains("/instances/"));
        await WaitForCircuitAsync();
        await _page.Locator($"a[href$='/{suffix}']").ClickAsync();
        await _page.WaitForURLAsync(url => url.Contains($"/{suffix}"));
        await WaitForCircuitAsync();
    }

    [Test]
    public async Task BackupChainCoverage_NoRequirements_IsUnknown_ThenMissingMemberIsIncomplete_NeverFalselyGreen()
    {
        await GoToInstanceSubPageAsync("ARXMAIL-PROD-01", "recovery");

        // No chain requirements recorded yet — Unknown, never a default green/Complete.
        await Assertions.Expect(_page.Locator("[data-testid=chain-coverage-status]")).ToContainTextAsync("Unknown");

        // Record a required member (Archival) that can never have matching evidence recorded
        // anywhere else in the app — a deliberately missing member.
        var chainCard = _page.Locator(".dbt-card", new PageLocatorOptions { HasText = "Backup-chain coverage" });
        await chainCard.Locator("select").SelectOptionAsync("Archival");
        await chainCard.GetByRole(AriaRole.Button, new LocatorGetByRoleOptions { Name = "Add" }).ClickAsync();
        await _page.WaitForTimeoutAsync(600);

        await Assertions.Expect(_page.Locator(".dbt-toast").First).ToContainTextAsync("Archival");
        await Assertions.Expect(_page.Locator("[data-testid=chain-coverage-status]")).ToContainTextAsync("Incomplete");
        await Assertions.Expect(_page.Locator("[data-testid=chain-missing-reasons]")).ToContainTextAsync("Archival");

        // The status must never silently read Complete/green with a known-missing member.
        await Assertions.Expect(_page.Locator("[data-testid=chain-coverage-status]")).Not.ToContainTextAsync("Complete");
    }

    [Test]
    public async Task BackupCopyLocation_AccessibleAndCannotVerify_AreDistinctStates()
    {
        await GoToInstanceSubPageAsync("ARXMAIL-DR-01", "recovery");

        var copyCard = _page.Locator(".dbt-card", new PageLocatorOptions { HasText = "Backup-copy availability" });
        await copyCard.Locator("input[placeholder='Label']").FillAsync("Primary NAS (E2E)");
        await copyCard.Locator("input[placeholder='Storage path/URL/bucket reference']").FillAsync(@"\\nas01\backups\arxmail-dr");
        await copyCard.GetByRole(AriaRole.Button, new LocatorGetByRoleOptions { Name = "Record copy location" }).ClickAsync();
        await _page.WaitForTimeoutAsync(600);

        var row = _page.Locator("[data-testid=copy-location-row]", new PageLocatorOptions { HasText = "Primary NAS (E2E)" });
        await Assertions.Expect(row.Locator("[data-testid=copy-verification-status]")).ToContainTextAsync("NeverVerified");

        await row.GetByRole(AriaRole.Button, new LocatorGetByRoleOptions { Name = "Mark accessible" }).ClickAsync();
        await _page.WaitForTimeoutAsync(600);
        var rowAfterAccessible = _page.Locator("[data-testid=copy-location-row]", new PageLocatorOptions { HasText = "Primary NAS (E2E)" });
        await Assertions.Expect(rowAfterAccessible.Locator("[data-testid=copy-verification-status]")).ToContainTextAsync("Accessible");

        await rowAfterAccessible.GetByRole(AriaRole.Button, new LocatorGetByRoleOptions { Name = "Mark cannot verify" }).ClickAsync();
        await _page.WaitForTimeoutAsync(600);
        var rowAfterCannotVerify = _page.Locator("[data-testid=copy-location-row]", new PageLocatorOptions { HasText = "Primary NAS (E2E)" });
        await Assertions.Expect(rowAfterCannotVerify.Locator("[data-testid=copy-verification-status]")).ToContainTextAsync("CannotVerify");
        await Assertions.Expect(rowAfterCannotVerify.Locator("[data-testid=copy-verification-status]")).Not.ToContainTextAsync("Accessible");
    }

    [Test]
    public async Task IntegrityCheckResult_ShownDistinctFromBackupHealth()
    {
        // Capture the instance's ordinary backup-health badge before recording any integrity evidence.
        await GoToInstanceSubPageAsync("CONTOSO-HR-MYSQL", "assurance");
        var detailUrl = _page.Url.Replace("/assurance", "");
        await GotoAsync(new Uri(detailUrl).PathAndQuery);
        await WaitForCircuitAsync();
        var healthBefore = await _page.Locator(".badge-health").First.InnerTextAsync();

        await GotoAsync(new Uri(detailUrl).PathAndQuery + "/assurance");
        await WaitForCircuitAsync();

        var maintenanceCard = _page.Locator(".dbt-card", new PageLocatorOptions { HasText = "Integrity, replication/failover" });
        await maintenanceCard.Locator("select").First.SelectOptionAsync("IntegrityCheck");
        await maintenanceCard.Locator("input[placeholder='Node/deployment label']").FillAsync("primary-node (E2E)");
        var outcomeSelect = maintenanceCard.Locator("select").Nth(1);
        await outcomeSelect.SelectOptionAsync("Failed");
        await maintenanceCard.GetByRole(AriaRole.Button, new LocatorGetByRoleOptions { Name = "Record result", Exact = true }).ClickAsync();
        await _page.WaitForTimeoutAsync(600);

        await Assertions.Expect(_page.Locator(".dbt-toast").First).ToContainTextAsync("Failed");
        var row = _page.Locator("[data-testid=maintenance-evidence-row]", new PageLocatorOptions { HasText = "primary-node (E2E)" });
        await Assertions.Expect(row.Locator("[data-testid=maintenance-evidence-status]")).ToContainTextAsync("Failed");

        // Backup health, computed entirely separately, must be completely unaffected.
        await GotoAsync(new Uri(detailUrl).PathAndQuery);
        await WaitForCircuitAsync();
        var healthAfter = await _page.Locator(".badge-health").First.InnerTextAsync();
        Assert.That(healthAfter, Is.EqualTo(healthBefore));
    }

    [Test]
    public async Task CapacityMeasurement_ForecastAlwaysStatesItsAssumptions()
    {
        await GoToInstanceSubPageAsync("EDIQRA-INVENTORY-PG", "assurance");

        var capacityCard = _page.Locator(".dbt-card", new PageLocatorOptions { HasText = "Capacity and performance planning" });

        // A single measurement is never enough for a projection, but assumptions/gaps must still show.
        await Assertions.Expect(_page.Locator("[data-testid=forecast-missing-data]")).ToBeVisibleAsync();
        await Assertions.Expect(_page.Locator("[data-testid=forecast-assumptions]")).ToContainTextAsync("linear growth");

        await capacityCard.Locator("select").First.SelectOptionAsync("Growth");
        await capacityCard.Locator("input[placeholder='Value']").FillAsync("512");
        await capacityCard.Locator("input[placeholder='Unit (GB, IOPS, ms…)']").FillAsync("GB");
        await capacityCard.GetByRole(AriaRole.Button, new LocatorGetByRoleOptions { Name = "Record measurement" }).ClickAsync();
        await _page.WaitForTimeoutAsync(600);

        await Assertions.Expect(_page.Locator(".dbt-toast").First).ToContainTextAsync("recorded");
        await Assertions.Expect(_page.Locator("[data-testid=capacity-measurement-table]")).ToContainTextAsync("512.0 GB");

        // Still only one data point — the forecast must keep stating it lacks enough data, not guess.
        await Assertions.Expect(_page.Locator("[data-testid=forecast-missing-data]")).ToBeVisibleAsync();
        await Assertions.Expect(_page.Locator("[data-testid=forecast-assumptions]")).ToContainTextAsync("Does not account for seasonal spikes");
    }

    [Test]
    public async Task CertificateExpiryReminder_ShowsExpiringSoon_AndCsvExportNeverLeaksTheSecretValue()
    {
        await GoToInstanceSubPageAsync("AVALOKH-SALES-SQL", "certificates");

        const string secretPlanted = "hunter2PlantedSecretValue";
        var expiresSoon = DateTime.UtcNow.AddDays(10).ToString("yyyy-MM-dd");

        await _page.Locator("input[placeholder='Label']").FillAsync("Prod TLS cert (E2E)");
        await _page.Locator("input[type='date']").FillAsync(expiresSoon);
        await _page.Locator("input[placeholder*='Secret-store reference']").FillAsync($"password={secretPlanted}");
        await _page.GetByRole(AriaRole.Button, new PageGetByRoleOptions { Name = "Record dependency" }).ClickAsync();
        await _page.WaitForTimeoutAsync(600);

        await Assertions.Expect(_page.Locator(".dbt-toast").First).ToContainTextAsync("recorded");

        var row = _page.Locator("[data-testid=certificate-row]", new PageLocatorOptions { HasText = "Prod TLS cert (E2E)" });
        await Assertions.Expect(row.Locator("[data-testid=certificate-expiry-status]")).ToContainTextAsync("ExpiringSoon");

        // The raw secret value must never have been persisted/rendered anywhere on the page.
        await Assertions.Expect(_page.Locator("body")).Not.ToContainTextAsync(secretPlanted);

        var downloadTask = _page.WaitForDownloadAsync();
        await _page.ClickAsync("[data-testid=export-certificates-csv]");
        var download = await downloadTask;

        Assert.That(download.SuggestedFilename, Does.StartWith("dbtracker-certificates-"));
        var path = await download.PathAsync();
        Assert.That(path, Is.Not.Null);
        var csvContent = await File.ReadAllTextAsync(path);

        Assert.That(csvContent, Does.Not.Contain(secretPlanted));
        Assert.That(csvContent, Does.Contain("redacted"));
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
}
