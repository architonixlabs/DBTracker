using System.Text;
using System.Text.RegularExpressions;
using Microsoft.Playwright;

namespace DBTracker.E2E;

/// <summary>G15: change freezes, runbook steps and backdated journal entries (D12), the lifecycle export's denominator (D15), required checks
/// and evidence imports (D18/D19), and certificate renewal history (D20).</summary>
[TestFixture]
public class G15ChangeRecoveryAssuranceTests
{
    private IPage _page = null!;

    [SetUp]
    public async Task SetUp()
    {
        _page = await GlobalSetup.Server.Browser.NewPageAsync();
    }

    [TearDown]
    public async Task TearDown() => await _page.CloseAsync();

    [Test]
    public async Task ChangeFreeze_BlocksApproval_RunbookStepsAndBackdatedJournalAreRecorded()
    {
        var freezeName = $"G15 freeze {Guid.NewGuid():N}"[..24];
        var freezeStart = DateTime.UtcNow.AddDays(70).Date;

        // The IT manager declares a freeze; a DBA can't.
        await LoginAsync("marcus.chen@architonixlabs.com");
        await GotoAsync("/change-freezes");
        await WaitForCircuitAsync();
        await _page.Locator("[data-testid=freeze-name]").FillAsync(freezeName);
        await _page.Locator("[data-testid=freeze-start]").FillAsync(freezeStart.ToString("yyyy-MM-ddTHH:mm"));
        await _page.Locator("[data-testid=freeze-end]").FillAsync(freezeStart.AddDays(2).ToString("yyyy-MM-ddTHH:mm"));
        await _page.Locator("[data-testid=create-freeze-button]").ClickAsync();
        await _page.WaitForTimeoutAsync(500);
        var freezeRow = _page.Locator("[data-testid=freeze-row]", new PageLocatorOptions { HasText = freezeName });
        await Assertions.Expect(freezeRow).ToHaveCountAsync(1);

        try
        {
            await LoginAsync("priya.nair@architonixlabs.com");
            await GotoAsync("/change-freezes");
            await WaitForCircuitAsync();
            await Assertions.Expect(_page.Locator("[data-testid=create-freeze-button]")).ToHaveCountAsync(0);

            var campaignId = await CreateCampaignAsync($"G15 freeze campaign {Guid.NewGuid():N}"[..40], "AVALOKH-SALES-SQL");
            await GotoAsync($"/campaigns/{campaignId}/change-windows");
            await WaitForCircuitAsync();
            var start = freezeStart.AddHours(10);
            await _page.FillAsync("input[placeholder='Title']", "Window inside freeze");
            await _page.Locator("input[type=datetime-local]").First.FillAsync(start.ToString("yyyy-MM-ddTHH:mm"));
            await _page.Locator("input[type=datetime-local]").Nth(1).FillAsync(start.AddHours(2).ToString("yyyy-MM-ddTHH:mm"));
            await _page.Locator("[data-testid=check-conflicts-button]").ClickAsync();
            await _page.WaitForTimeoutAsync(500);
            await Assertions.Expect(_page.Locator("[data-testid=freeze-conflicts]")).ToContainTextAsync(freezeName);

            // Planning is still allowed.
            await _page.Locator("[data-testid=create-window-button]").ClickAsync();
            await _page.WaitForTimeoutAsync(500);
            await _page.Locator("[data-testid=change-window-row]").First.Locator("a:has-text('Open')").ClickAsync();
            await _page.WaitForURLAsync(url => url.Contains("/change-windows/"));
            await WaitForCircuitAsync();
            await Assertions.Expect(_page.Locator("[data-testid=window-conflicts]")).ToContainTextAsync(freezeName);

            // Even with its approval recorded, the window can't be approved inside the freeze.
            await _page.Locator("#changewindow-approval").SelectOptionAsync(new SelectOptionValue { Index = 1 });
            await _page.Locator("[data-testid=record-approval-button]").ClickAsync();
            await _page.WaitForTimeoutAsync(500);
            await _page.Locator("[data-testid=status-Approved]").ClickAsync();
            await _page.WaitForTimeoutAsync(500);
            await Assertions.Expect(_page.Locator("[data-testid=status-error]")).ToContainTextAsync("change freeze");
            await Assertions.Expect(_page.Locator("[data-testid=change-window-detail-status]")).ToHaveTextAsync("Planned");

            // Runbook steps can be added and ticked off.
            await _page.Locator("[data-testid=runbook-new-step]").FillAsync("Stop application writes");
            await _page.Locator("[data-testid=add-runbook-step-button]").ClickAsync();
            await _page.WaitForTimeoutAsync(500);
            await _page.Locator("[data-testid=runbook-step-checkbox]").First.CheckAsync();
            await _page.WaitForTimeoutAsync(500);
            await Assertions.Expect(_page.Locator("[data-testid=runbook-progress]")).ToContainTextAsync("1 of 1 done");

            // A journal entry records when it happened, not when it was typed.
            var happened = DateTime.UtcNow.AddHours(-3);
            await _page.Locator("[data-testid=journal-time-input]").FillAsync(happened.ToString("yyyy-MM-ddTHH:mm"));
            await _page.FillAsync("input[placeholder='Description']", "Writes stopped at the source");
            await _page.Locator("[data-testid=add-journal-entry-button]").ClickAsync();
            await _page.WaitForTimeoutAsync(500);
            await Assertions.Expect(_page.Locator("[data-testid=journal-time]").First).ToHaveTextAsync(happened.ToString("yyyy-MM-dd HH:mm"));

            await _page.Locator("[data-testid=journal-time-input]").FillAsync(DateTime.UtcNow.AddDays(1).ToString("yyyy-MM-ddTHH:mm"));
            await _page.FillAsync("input[placeholder='Description']", "Typed ahead of time");
            await _page.Locator("[data-testid=add-journal-entry-button]").ClickAsync();
            await _page.WaitForTimeoutAsync(500);
            await Assertions.Expect(_page.Locator("[data-testid=journal-error]")).ToContainTextAsync("future");
        }
        finally
        {
            // Lift the freeze so it can't affect other tests sharing this database.
            await LoginAsync("marcus.chen@architonixlabs.com");
            await GotoAsync("/change-freezes");
            await WaitForCircuitAsync();
            await _page.Locator("[data-testid=freeze-row]", new PageLocatorOptions { HasText = freezeName }).Locator("[data-testid=lift-freeze-button]").ClickAsync();
            await _page.WaitForTimeoutAsync(500);
        }
    }

    [Test]
    public async Task LifecycleExport_StartsWithTheDenominatorAndFreshness()
    {
        await LoginAsync("angela.price@contoso.com");
        await GotoAsync("/lifecycle/workspace");
        await WaitForCircuitAsync();

        var downloadTask = _page.WaitForDownloadAsync();
        await _page.Locator("[data-testid=export-workspace-button]").ClickAsync();
        var download = await downloadTask;
        var csv = await File.ReadAllTextAsync((await download.PathAsync())!);

        Assert.That(csv, Does.Contain("# Rows in this file:"));
        Assert.That(csv, Does.Contain("non-archived instances (the denominator)"));
        Assert.That(csv, Does.Contain("# Oldest identity observation:"));
        Assert.That(csv, Does.Contain("IdentityObservedAtUtc"));
    }

    [Test]
    public async Task RequiredCheck_NeverRecorded_IsMissing_AndCapacityImportIsRepeatSafe()
    {
        await LoginAsync("priya.nair@architonixlabs.com");
        var instanceName = "EDIQRA-INVENTORY-PG";
        await GoToInstanceSubPageAsync(instanceName, "assurance");

        await _page.Locator("[data-testid=req-check-type]").SelectOptionAsync("FailoverDrill");
        await _page.Locator("[data-testid=add-required-check]").ClickAsync();
        await _page.WaitForTimeoutAsync(500);
        var reqRow = _page.Locator("[data-testid=required-check-row]", new PageLocatorOptions { HasText = "FailoverDrill" });
        await Assertions.Expect(reqRow.Locator("[data-testid=required-check-status]")).ToContainTextAsync("Missing");

        var measuredAt = DateTime.UtcNow.AddDays(-2).ToString("yyyy-MM-ddTHH:mm:ssZ");
        var value = Random.Shared.Next(100, 900);
        var csv = $"Instance,MeasurementType,MeasuredAtUtc,Value,Unit,Source\n{instanceName},Growth,{measuredAt},{value},GB,G15 E2E export\n";

        await GotoAsync("/assurance/import");
        await WaitForCircuitAsync();
        await _page.Locator("[data-testid=import-kind-capacity]").CheckAsync();
        await _page.SetInputFilesAsync("[data-testid=assurance-import-file]", new FilePayload { Name = "capacity.csv", MimeType = "text/csv", Buffer = Encoding.UTF8.GetBytes(csv) });
        await _page.WaitForTimeoutAsync(800);
        await Assertions.Expect(_page.Locator("[data-testid=assurance-import-counts]")).ToContainTextAsync("1 new");
        await _page.Locator("[data-testid=apply-assurance-import]").ClickAsync();
        await _page.WaitForTimeoutAsync(800);

        // The preview is rebuilt after applying: the same row is now already recorded.
        await Assertions.Expect(_page.Locator("[data-testid=assurance-import-counts]")).ToContainTextAsync("0 new");
        await Assertions.Expect(_page.Locator("[data-testid=assurance-import-counts]")).ToContainTextAsync("1 already recorded");

        await GoToInstanceSubPageAsync(instanceName, "assurance");
        await Assertions.Expect(_page.Locator("[data-testid=capacity-measurement-table]")).ToContainTextAsync($"{value:N1} GB");
    }

    [Test]
    public async Task CertificateRenewal_UpdatesExpiry_AndKeepsHistory()
    {
        await LoginAsync("priya.nair@architonixlabs.com");
        await GoToInstanceSubPageAsync("CONTOSO-HR-MYSQL", "certificates");

        var label = $"G15 TLS {Guid.NewGuid():N}"[..16];
        await _page.Locator("input[placeholder='Label']").FillAsync(label);
        await _page.Locator("input[type='date']").FillAsync(DateTime.UtcNow.AddDays(20).ToString("yyyy-MM-dd"));
        await _page.Locator("[data-testid=certificate-owner-email]").FillAsync("cert.owner@example.com");
        await _page.Locator("[data-testid=add-certificate]").ClickAsync();
        await _page.WaitForTimeoutAsync(500);

        var row = _page.Locator("[data-testid=certificate-row]", new PageLocatorOptions { HasText = label });
        await Assertions.Expect(row.Locator("[data-testid=certificate-expiry-status]")).ToContainTextAsync("ExpiringSoon");

        await row.Locator("[data-testid=renew-certificate-button]").ClickAsync();
        var newExpiry = DateTime.UtcNow.AddDays(400);
        await _page.Locator("[data-testid=certificate-new-expiry]").FillAsync(newExpiry.ToString("yyyy-MM-dd"));
        await _page.Locator("#cert-event-evidence").FillAsync("CHG-4411");
        await _page.Locator("[data-testid=save-certificate-event]").ClickAsync();
        await _page.WaitForTimeoutAsync(500);

        await Assertions.Expect(row.Locator("[data-testid=certificate-expiry-status]")).ToContainTextAsync("Ok");
        await Assertions.Expect(_page.Locator("[data-testid=certificate-history-item]").First).ToContainTextAsync(newExpiry.ToString("yyyy-MM-dd"));
        await Assertions.Expect(_page.Locator("[data-testid=certificate-history-item]").First).ToContainTextAsync("CHG-4411");
    }

    private async Task GoToInstanceSubPageAsync(string instanceSearchText, string suffix)
    {
        await GotoAsync($"/instances?s={instanceSearchText}");
        await _page.WaitForSelectorAsync(".dbt-table tbody tr");
        var detailHref = await _page.Locator("a.instance-row-link").First.GetAttributeAsync("href");
        await GotoAsync(detailHref! + "/" + suffix);
        await WaitForCircuitAsync();
    }

    private async Task<int> CreateCampaignAsync(string name, string instanceLabelText)
    {
        await GotoAsync("/campaigns");
        await WaitForCircuitAsync();

        await _page.FillAsync("input[placeholder='Campaign name']", name);
        await _page.FillAsync("input[placeholder='Source label']", "Source");
        await _page.FillAsync("input[placeholder='Target label']", "Target");
        await _page.Locator("label", new PageLocatorOptions { HasText = instanceLabelText }).ClickAsync();
        await _page.GetByRole(AriaRole.Button, new PageGetByRoleOptions { Name = "Create campaign" }).ClickAsync();
        await _page.WaitForURLAsync(url => url.Contains("/campaigns/"), new PageWaitForURLOptions { Timeout = 15000 });
        await WaitForCircuitAsync();

        return int.Parse(Regex.Match(_page.Url, @"/campaigns/(\d+)").Groups[1].Value);
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
}
