using Microsoft.Playwright;

namespace DBTracker.E2E;

/// <summary>
/// E2E coverage for Epic 17's U20: keyboard operability, ARIA semantics and announced feedback,
/// without replacing the existing dark ops-console theme. Covers one dropdown (the Instances list's
/// "Views" menu) and one form (the Instances filter bar's named inputs) per the task's minimum, plus
/// keyboard-operable column sorting and a live-region toast check.
/// </summary>
[TestFixture]
public class Epic17AccessibilityTests
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
    public async Task ColumnSortHeader_IsARealButton_OperableByKeyboardAlone()
    {
        await GotoAsync("/instances");
        await _page.WaitForSelectorAsync(".dbt-table table");
        await WaitForCircuitAsync();

        var sortButton = _page.Locator("button.th-sort-btn", new PageLocatorOptions { HasText = "Health" });
        await Assertions.Expect(sortButton).ToBeVisibleAsync();

        // Focus it directly (as a keyboard user tabbing through would land on it — a real <button>
        // is natively focusable and triggerable via Enter, unlike the old role="button" <th>) and
        // activate with Enter rather than a mouse click.
        await sortButton.FocusAsync();
        await Assertions.Expect(sortButton).ToBeFocusedAsync();
        await _page.Keyboard.PressAsync("Enter");
        await _page.WaitForTimeoutAsync(400);

        Assert.That(_page.Url, Does.Contain("sort=Risk"));
    }

    [Test]
    public async Task ViewsDropdown_EscapeCloses_AndReturnsFocusToItsToggleButton()
    {
        await GotoAsync("/instances");
        await _page.WaitForSelectorAsync(".dbt-table table");
        await WaitForCircuitAsync();

        var toggle = _page.Locator("button:has-text('Views')");
        await toggle.ClickAsync();
        await Assertions.Expect(_page.Locator("[role=menu][aria-label='Saved views']")).ToBeVisibleAsync();

        await _page.Keyboard.PressAsync("Escape");
        await _page.WaitForTimeoutAsync(200);

        await Assertions.Expect(_page.Locator("[role=menu][aria-label='Saved views']")).Not.ToBeVisibleAsync();
        await Assertions.Expect(toggle).ToBeFocusedAsync();
    }

    [Test]
    public async Task NotificationBell_EscapeClosesDropdown_AndReturnsFocus()
    {
        await GotoAsync("/");
        await WaitForCircuitAsync();

        var bell = _page.Locator("button.notif-bell-btn");
        await bell.ClickAsync();
        await Assertions.Expect(_page.Locator("[role=menu][aria-label=Notifications]")).ToBeVisibleAsync();

        await _page.Keyboard.PressAsync("Escape");
        await _page.WaitForTimeoutAsync(200);

        await Assertions.Expect(_page.Locator("[role=menu][aria-label=Notifications]")).Not.ToBeVisibleAsync();
        await Assertions.Expect(bell).ToBeFocusedAsync();
    }

    [Test]
    public async Task FilterBarInputs_AreReachableByTheirAccessibleLabel()
    {
        await GotoAsync("/instances");
        await _page.WaitForSelectorAsync(".dbt-table table");
        await WaitForCircuitAsync();

        // GetByLabel resolves via the <label for="..."> association (visually hidden, not merely a
        // placeholder) — if the label wiring regresses, these lookups fail even though the controls
        // are still visibly present on screen. Exact: true avoids matching an unrelated sort
        // button's aria-label (e.g. "Sort by engine" also contains "Engine").
        await Assertions.Expect(_page.GetByLabel("Search instances", new PageGetByLabelOptions { Exact = true })).ToBeVisibleAsync();
        await Assertions.Expect(_page.GetByLabel("Engine", new PageGetByLabelOptions { Exact = true })).ToBeVisibleAsync();
        await Assertions.Expect(_page.GetByLabel("Health", new PageGetByLabelOptions { Exact = true })).ToBeVisibleAsync();
        await Assertions.Expect(_page.GetByLabel("Environment", new PageGetByLabelOptions { Exact = true })).ToBeVisibleAsync();
    }

    [Test]
    public async Task Toast_UsesALiveRegionRole_SoScreenReadersAnnounceItWithoutAVisualChange()
    {
        // Logging a backup is one of the core tasks the acceptance criteria names explicitly —
        // its toast confirmation must be announced to assistive tech, not purely visual. Navigates
        // via the semantic instance link's own href (U20) rather than a row click, so this test
        // doesn't depend on Playwright's implicit post-click navigation wait.
        await GotoAsync("/instances?s=ARXMAIL-PROD-01");
        await _page.WaitForSelectorAsync(".dbt-table tbody tr");
        var detailHref = await _page.Locator("a.instance-row-link").First.GetAttributeAsync("href");
        await GotoAsync(detailHref!);
        await WaitForCircuitAsync();

        await _page.ClickAsync("button:has-text('Log backup')");
        await _page.WaitForTimeoutAsync(500);

        var toast = _page.Locator(".dbt-toast").First;
        await Assertions.Expect(toast).ToBeVisibleAsync();
        await Assertions.Expect(toast).ToHaveAttributeAsync("role", "status");
        await Assertions.Expect(toast).ToHaveAttributeAsync("aria-live", "polite");
    }

    private Task LoginAsync(string email) => LoginAsync(email, "ArxDemo#2026!");

    private async Task LoginAsync(string email, string password)
    {
        await GotoAsync("/Account/Login");
        await _page.FillAsync("input[autocomplete=username]", email);
        await _page.FillAsync("input[autocomplete=current-password]", password);
        await _page.ClickAsync("button[type=submit]");
        await _page.WaitForURLAsync(url => !url.Contains("/Account/Login"), new PageWaitForURLOptions { Timeout = 15000 });
        await WaitForCircuitAsync();
    }

    private Task GotoAsync(string path) =>
        _page.GotoAsync(TestServerFixture.BaseUrl + path, new PageGotoOptions { WaitUntil = WaitUntilState.NetworkIdle });

    private Task WaitForCircuitAsync() => _page.WaitForTimeoutAsync(800);
}
