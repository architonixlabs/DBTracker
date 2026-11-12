using Microsoft.Playwright;

namespace DBTracker.E2E;

/// <summary>
/// G32 follow-ups: manual backup entries reach the audit trail with the values entered, and a maintenance result can be
/// recorded for a check that already ran (in a stated zone) but never for the future.
/// </summary>
[TestFixture]
public class G32FollowUpTests
{
    private IPage _page = null!;

    [SetUp]
    public async Task SetUp() => _page = await GlobalSetup.Server.Browser.NewPageAsync();

    [TearDown]
    public async Task TearDown() => await _page.CloseAsync();

    [Test]
    public async Task ManualBackupEntry_IsAudited_WithTheEnteredValues()
    {
        await LoginAsync("priya.nair@architonixlabs.com");
        var instanceName = await OpenFirstInstanceAsync("ARXMAIL-PROD-01");

        await _page.Locator("h2:has-text('Log a backup result')").ScrollIntoViewIfNeededAsync();
        await _page.Locator("#backup-occurred").FillAsync(DateTime.Now.AddDays(-2).ToString("yyyy-MM-ddTHH:mm"));
        await _page.Locator("button[type=submit]:has-text('Log backup')").ClickAsync();
        await Assertions.Expect(_page.Locator(".dbt-toast").First).ToContainTextAsync("Backup result logged", new LocatorAssertionsToContainTextOptions { Timeout = 10000 });

        await LoginAsync("angela.price@contoso.com");
        await GotoAsync("/audit");
        await _page.WaitForSelectorAsync("tbody tr");
        await WaitForCircuitAsync();
        await _page.FillAsync("#audit-search", "BackupRecordLogged");
        await _page.WaitForTimeoutAsync(900);

        var row = _page.Locator("tbody tr", new PageLocatorOptions { HasText = "BackupRecordLogged" }).First;
        await Assertions.Expect(row).ToBeVisibleAsync();
        await Assertions.Expect(row).ToContainTextAsync(instanceName);
        await Assertions.Expect(row).ToContainTextAsync("Manual entry");
    }

    [Test]
    public async Task MaintenanceResult_CanBeBackdatedInUtc_AndAFutureTimeIsRejected()
    {
        await LoginAsync("priya.nair@architonixlabs.com");
        await OpenFirstInstanceAsync("CONTOSO-HR-MYSQL");
        await _page.Locator("a[href$='/assurance']").ClickAsync();
        await _page.WaitForURLAsync(url => url.Contains("/assurance"));
        await WaitForCircuitAsync();

        var card = _page.Locator(".dbt-card", new PageLocatorOptions { HasText = "Integrity, replication/failover" });
        var node = $"g32-node-{Guid.NewGuid():N}"[..16];
        await card.Locator("input[placeholder='Node/deployment label']").FillAsync(node);
        await card.Locator("[data-testid=maintenance-ran-at-zone]").SelectOptionAsync("UTC");

        // A future run time is refused and nothing is recorded.
        var tomorrowNoonUtc = DateTime.UtcNow.Date.AddDays(1).AddHours(12);
        await card.Locator("[data-testid=maintenance-ran-at]").FillAsync(tomorrowNoonUtc.ToString("yyyy-MM-ddTHH:mm"));
        await card.Locator("[data-testid=record-maintenance-evidence]").ClickAsync();
        await Assertions.Expect(card.Locator("[role=alert]")).ToContainTextAsync("future");
        await Assertions.Expect(_page.Locator("[data-testid=maintenance-evidence-row]", new PageLocatorOptions { HasText = node })).ToHaveCountAsync(0);

        // Noon UTC three days ago is the same calendar day in every zone the test browser can be in.
        var pastNoonUtc = DateTime.UtcNow.Date.AddDays(-3).AddHours(12);
        await card.Locator("[data-testid=maintenance-ran-at]").FillAsync(pastNoonUtc.ToString("yyyy-MM-ddTHH:mm"));
        await card.Locator("[data-testid=record-maintenance-evidence]").ClickAsync();
        await Assertions.Expect(_page.Locator(".dbt-toast").First).ToContainTextAsync("result recorded", new LocatorAssertionsToContainTextOptions { Timeout = 10000 });

        var row = _page.Locator("[data-testid=maintenance-evidence-row]", new PageLocatorOptions { HasText = node });
        await Assertions.Expect(row).ToContainTextAsync(pastNoonUtc.ToString("MMM d, yyyy", System.Globalization.CultureInfo.InvariantCulture));
    }

    /// <summary>G34: capacity measurements take a time in a stated zone, like maintenance results, instead of an unlabelled UTC field.</summary>
    [Test]
    public async Task CapacityMeasurement_CanBeBackdatedInUtc_AndAFutureTimeIsRejected()
    {
        await LoginAsync("priya.nair@architonixlabs.com");
        await OpenFirstInstanceAsync("CONTOSO-HR-MYSQL");
        await _page.Locator("a[href$='/assurance']").ClickAsync();
        await _page.WaitForURLAsync(url => url.Contains("/assurance"));
        await WaitForCircuitAsync();

        var card = _page.Locator(".dbt-card", new PageLocatorOptions { HasText = "Capacity and performance planning evidence" });
        var source = $"g34-cap-{Guid.NewGuid():N}"[..16];
        await card.Locator("#cap-value").FillAsync("123.4");
        await card.Locator("#cap-source").FillAsync(source);
        await card.Locator("[data-testid=capacity-measured-at-zone]").SelectOptionAsync("UTC");
        await Assertions.Expect(card.Locator("label[for=cap-measured-at]")).ToContainTextAsync("UTC");

        var tomorrowNoonUtc = DateTime.UtcNow.Date.AddDays(1).AddHours(12);
        await card.Locator("[data-testid=capacity-measured-at]").FillAsync(tomorrowNoonUtc.ToString("yyyy-MM-ddTHH:mm"));
        await card.Locator("[data-testid=add-capacity-measurement]").ClickAsync();
        await Assertions.Expect(card.Locator("[role=alert]")).ToContainTextAsync("future");
        await Assertions.Expect(card.Locator("[data-testid=capacity-measurement-row]", new PageLocatorOptions { HasText = source })).ToHaveCountAsync(0);

        var pastNoonUtc = DateTime.UtcNow.Date.AddDays(-3).AddHours(12);
        await card.Locator("[data-testid=capacity-measured-at]").FillAsync(pastNoonUtc.ToString("yyyy-MM-ddTHH:mm"));
        await card.Locator("[data-testid=add-capacity-measurement]").ClickAsync();
        await Assertions.Expect(_page.Locator(".dbt-toast").First).ToContainTextAsync("Capacity measurement recorded", new LocatorAssertionsToContainTextOptions { Timeout = 10000 });

        var row = card.Locator("[data-testid=capacity-measurement-row]", new PageLocatorOptions { HasText = source });
        await Assertions.Expect(row).ToContainTextAsync(pastNoonUtc.ToString("MMM d, yyyy", System.Globalization.CultureInfo.InvariantCulture));
    }

    private async Task<string> OpenFirstInstanceAsync(string search)
    {
        await GotoAsync($"/instances?s={search}");
        await _page.WaitForSelectorAsync(".dbt-table tbody tr");
        await WaitForCircuitAsync();
        var link = _page.Locator(".instance-row-link").First;
        var name = (await link.InnerTextAsync()).Trim();
        await link.ClickAsync();
        await _page.WaitForURLAsync(url => url.Contains("/instances/"));
        await WaitForCircuitAsync();
        return name;
    }

    private async Task LoginAsync(string email)
    {
        await GotoAsync("/Account/Login");
        await _page.FillAsync("input[autocomplete~=username]", email);
        await _page.FillAsync("input[autocomplete=current-password]", "ArxDemo#2026!");
        await _page.ClickAsync("button[type=submit]:has-text('Log in')");
        await _page.WaitForURLAsync(url => !url.Contains("/Account/Login"), new PageWaitForURLOptions { Timeout = 15000 });
        await WaitForCircuitAsync();
    }

    private Task GotoAsync(string path) =>
        _page.GotoAsync(TestServerFixture.BaseUrl + path, new PageGotoOptions { WaitUntil = WaitUntilState.Load });

    private Task WaitForCircuitAsync() => _page.WaitForTimeoutAsync(800);
}
