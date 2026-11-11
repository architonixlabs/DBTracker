using Microsoft.Playwright;

namespace DBTracker.E2E;

/// <summary>E2E coverage for Epic 14's U05 (shareable/saved filtered views) and U06 (bounded, query-driven Instances list).</summary>
[TestFixture]
public class SavedViewsAndPagingTests
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

    [Test]
    public async Task ChangingAFilter_UpdatesTheUrl_SoTheViewIsShareable()
    {
        await GotoAsync("/instances");
        await _page.WaitForSelectorAsync(".dbt-table table");
        await WaitForCircuitAsync();

        // The select itself doesn't carry a stable accessible name, so select by its default option text instead.
        await _page.Locator("select").Filter(new LocatorFilterOptions { HasText = "All engines" }).SelectOptionAsync("PostgreSql");
        await _page.WaitForTimeoutAsync(600);

        Assert.That(_page.Url, Does.Contain("engine=PostgreSql"));

        // A plain reload of that same URL must reproduce the same filtered result (it's not just
        // client-side state) — the whole point of U05 is that the URL alone is enough to share.
        await _page.ReloadAsync();
        await WaitForCircuitAsync();
        var rows = _page.Locator(".dbt-table tbody tr");
        var count = await rows.CountAsync();
        Assert.That(count, Is.GreaterThan(0));
        for (var i = 0; i < count; i++)
        {
            await Assertions.Expect(rows.Nth(i)).ToContainTextAsync("PostgreSql");
        }
    }

    [Test]
    public async Task SavedView_CanBeSavedAndReappliedAfterReset()
    {
        var viewName = $"E2E view {Guid.NewGuid():N}".Substring(0, 20);

        await GotoAsync("/instances?engine=MySql");
        await WaitForCircuitAsync();

        await _page.ClickAsync("button:has-text('Views')");
        await _page.ClickAsync("button:has-text('Save current view')");
        await _page.FillAsync("input[placeholder='View name']", viewName);
        await _page.ClickAsync("button:has-text('Save')");
        await _page.WaitForTimeoutAsync(500);
        await Assertions.Expect(_page.Locator(".dbt-toast")).ToContainTextAsync("Saved view");

        // Reset to the default (unfiltered) view.
        await GotoAsync("/instances");
        await WaitForCircuitAsync();
        Assert.That(_page.Url, Does.Not.Contain("engine="));

        // Reapplying the saved view must restore the exact filter.
        await _page.ClickAsync("button:has-text('Views')");
        await _page.ClickAsync($"a:text('{viewName}')");
        await _page.WaitForURLAsync(url => url.Contains("engine=MySql"));
        await WaitForCircuitAsync();

        var rows = _page.Locator(".dbt-table tbody tr");
        var count = await rows.CountAsync();
        for (var i = 0; i < count; i++)
        {
            await Assertions.Expect(rows.Nth(i)).ToContainTextAsync("MySql");
        }
    }

    [Test]
    public async Task SortHeaderClick_TogglesOrder_AndPersistsInUrl()
    {
        await GotoAsync("/instances");
        await _page.WaitForSelectorAsync(".dbt-table table");
        await WaitForCircuitAsync();

        await _page.ClickAsync("th:has-text('Health')");
        await _page.WaitForTimeoutAsync(400);
        Assert.That(_page.Url, Does.Contain("sort=Risk"));

        var firstNameAsc = await _page.Locator(".dbt-table tbody tr").First.InnerTextAsync();

        await _page.ClickAsync("th:has-text('Health')");
        await _page.WaitForTimeoutAsync(400);
        Assert.That(_page.Url, Does.Contain("dir=desc"));

        var firstNameDesc = await _page.Locator(".dbt-table tbody tr").First.InnerTextAsync();
        Assert.That(firstNameDesc, Is.Not.EqualTo(firstNameAsc));
    }

    [Test]
    public async Task PageBeyondLastPage_ClampsInsteadOfShowingAnEmptyGrid()
    {
        await GotoAsync("/instances?page=999");
        await _page.WaitForSelectorAsync(".dbt-table table");
        await WaitForCircuitAsync();

        // Clamps to the last page, whichever that is once other tests have added instances: "Page N of N".
        await Assertions.Expect(_page.Locator(@"text=/Page (\d+) of \1\b/")).ToBeVisibleAsync();
        await Assertions.Expect(_page.Locator(".dbt-table tbody tr").First).ToBeVisibleAsync();
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
