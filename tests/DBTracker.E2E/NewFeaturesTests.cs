using System.Text;
using Microsoft.Playwright;

namespace DBTracker.E2E;

[TestFixture]
public class NewFeaturesTests
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
    public async Task MyInstancesOnly_NarrowsToContactEmailMatches()
    {
        // Priya (priya.nair@architonixlabs.com) is the contact on at least ARXMAIL-PROD-01,
        // ARXMAIL-DR-01 and NEXTCLOUD-FILES-MYSQL in seed data, among ten total instances.
        await GotoAsync("/instances");
        await _page.WaitForSelectorAsync(".dbt-table table");
        await WaitForCircuitAsync();

        var allCount = await _page.Locator(".dbt-table tbody tr").CountAsync();
        Assert.That(allCount, Is.GreaterThanOrEqualTo(10));

        await _page.ClickAsync("#my-instances");
        await _page.WaitForTimeoutAsync(400);

        var rows = _page.Locator(".dbt-table tbody tr");
        var myCount = await rows.CountAsync();
        Assert.That(myCount, Is.GreaterThanOrEqualTo(3));
        Assert.That(myCount, Is.LessThan(allCount));

        for (var i = 0; i < myCount; i++)
        {
            Assert.That(await rows.Nth(i).InnerTextAsync(), Does.Contain("priya.nair@architonixlabs.com"));
        }
    }

    [Test]
    public async Task MyInstancesCheckbox_HiddenForUserWithNoContactEmailMatch()
    {
        // Admin isn't the contact email on any seeded instance, so the checkbox shouldn't appear
        // at all rather than showing an always-empty, confusing filter.
        await LoginAsync("admin@architonixlabs.com");
        await GotoAsync("/instances");
        await _page.WaitForSelectorAsync(".dbt-table table");

        Assert.That(await _page.Locator("#my-instances").CountAsync(), Is.EqualTo(0));
    }

    [Test]
    public async Task MaintenanceWindow_StartAndEnd_ShowsAndClearsBadge()
    {
        await GotoAsync("/instances");
        await _page.WaitForSelectorAsync(".dbt-table table");
        await _page.ClickAsync(".dbt-table tbody tr >> nth=0");
        await _page.WaitForURLAsync(url => url.Contains("/instances/"));
        await WaitForCircuitAsync();

        await _page.ClickAsync("button:has-text('1 h')");
        await _page.WaitForTimeoutAsync(500);

        await Assertions.Expect(_page.Locator("text=Under maintenance")).ToBeVisibleAsync();
        await Assertions.Expect(_page.Locator("button:has-text('End maintenance now')")).ToBeVisibleAsync();

        await _page.ClickAsync("button:has-text('End maintenance now')");
        await _page.WaitForTimeoutAsync(500);

        Assert.That(await _page.Locator("text=Under maintenance").CountAsync(), Is.EqualTo(0));
        Assert.That(await _page.Locator("button:has-text('1 h')").CountAsync(), Is.EqualTo(1));
    }

    [Test]
    public async Task EditingInstance_RecordsChangeHistory()
    {
        var instanceName = $"E2E-AUDIT-{Guid.NewGuid():N}".Substring(0, 18).ToUpperInvariant();

        await GotoAsync("/instances/new");
        await _page.WaitForSelectorAsync("button[type=submit]");
        await WaitForCircuitAsync();
        await FillByLabel("Instance name", instanceName);
        await SelectByLabel("Engine", "PostgreSql");
        await _page.ClickAsync("button[type=submit]:text('Save')");
        await _page.WaitForURLAsync(url => url.Contains("/instances/"));
        await WaitForCircuitAsync();

        // No changes yet — created, not edited.
        await Assertions.Expect(_page.Locator("text=No changes recorded yet.")).ToBeVisibleAsync();

        var detailUrl = _page.Url;
        await _page.ClickAsync("a:has-text('Edit')");
        await _page.WaitForURLAsync(url => url.Contains("/edit"));
        await WaitForCircuitAsync();

        await FillByLabel("Application", "Audit Test App");
        await _page.ClickAsync("button[type=submit]:text('Save')");
        await _page.WaitForURLAsync(url => url == detailUrl);
        await WaitForCircuitAsync();

        // Change history is the last .dbt-table on the page (after backup/restore history).
        var changeHistory = _page.Locator(".dbt-table").Last;
        await Assertions.Expect(changeHistory).ToContainTextAsync("Application");
        await Assertions.Expect(changeHistory).ToContainTextAsync("Audit Test App");
        await Assertions.Expect(changeHistory).ToContainTextAsync("priya.nair@architonixlabs.com");
    }

    [Test]
    public async Task TestConnection_ShowsAResult()
    {
        await GotoAsync("/instances");
        await _page.WaitForSelectorAsync(".dbt-table table");
        await _page.ClickAsync(".dbt-table tbody tr >> nth=0");
        await _page.WaitForURLAsync(url => url.Contains("/instances/"));
        await WaitForCircuitAsync();

        await _page.ClickAsync("button:has-text('Test connection')");

        // Reachable or not, the page renders a verdict instead of hanging — either wording works.
        var locator = _page.Locator("text=Reachable").Or(_page.Locator("text=Not reachable"));
        await locator.First.WaitForAsync(new LocatorWaitForOptions { Timeout = 10000 });
    }

    [Test]
    public async Task ScanStalenessBanner_VisibleOnDashboard_LinksToReports()
    {
        // CONTOSO-HR-MYSQL is seeded with an errored scan, so the dashboard should surface it.
        await GotoAsync("/");
        await _page.WaitForSelectorAsync(".stat-strip");

        await Assertions.Expect(_page.Locator("text=scan(s) erroring, stale or never completed")).ToBeVisibleAsync();
        await _page.ClickAsync("a:has-text('View scan activity')");
        await _page.WaitForURLAsync(url => url.Contains("/reports"));
    }

    [Test]
    public async Task CsvImport_PreviewsAndImportsValidRows()
    {
        var instanceName = $"E2E-IMPORT-{Guid.NewGuid():N}".Substring(0, 18).ToUpperInvariant();
        var csv = $"Name,Engine,Application,Company\n{instanceName},PostgreSql,Imported App,Architonix Labs\n,MySql,Bad Row,Nowhere\n";

        await GotoAsync("/instances/import");
        await _page.WaitForSelectorAsync("input[type=file]");
        await WaitForCircuitAsync();

        await _page.SetInputFilesAsync("input[type=file]", new FilePayload
        {
            Name = "import.csv",
            MimeType = "text/csv",
            Buffer = Encoding.UTF8.GetBytes(csv)
        });

        // U17 superseded the old "X valid / X row(s) with errors" summary with an explicit
        // create/update/skip/ambiguous classification against the existing inventory.
        await _page.WaitForSelectorAsync("text=1 create");
        await Assertions.Expect(_page.Locator("text=1 row(s) with errors")).ToBeVisibleAsync();
        await Assertions.Expect(_page.Locator($"text={instanceName}")).ToBeVisibleAsync();

        await _page.ClickAsync("button:has-text('Apply')");
        await _page.WaitForURLAsync(url => url.Contains("/instances") && !url.Contains("/import"));
        await WaitForCircuitAsync();

        await _page.FillAsync("input[placeholder*='Search']", instanceName);
        await _page.WaitForTimeoutAsync(500);
        await Assertions.Expect(_page.Locator($"text={instanceName}")).ToBeVisibleAsync();
    }

    [Test]
    public async Task CsvImport_RouteBlockedForViewerRole()
    {
        await LoginAsync("dev.patel@ediqra.com");
        await GotoAsync("/instances/import");
        await _page.WaitForURLAsync(url => url.Contains("/Account/AccessDenied"));
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
