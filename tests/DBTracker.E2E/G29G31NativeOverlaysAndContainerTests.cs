using Microsoft.Playwright;

namespace DBTracker.E2E;

/// <summary>
/// G29 (native modal dialogs for consequential confirmations), G30 (Popover API for the notification
/// menu and toggletips in place of title-only tooltips) and G31 (container queries for the stat strip,
/// detail cards and key-value lists). Alex needs focus kept and returned; Noor needs it to fit at
/// 360px and at 200% zoom (a 640px-wide viewport is what a 1280px window shows at 200%).
/// </summary>
[TestFixture]
public class G29G31NativeOverlaysAndContainerTests
{
    private IPage _page = null!;

    [SetUp]
    public async Task SetUp()
    {
        _page = await GlobalSetup.Server.Browser.NewPageAsync();
        await LoginAsync("admin@architonixlabs.com");
    }

    [TearDown]
    public async Task TearDown() => await _page.CloseAsync();

    [Test]
    public async Task ArchiveConfirm_IsANamedModalDialog_EscapeCancels_AndFocusReturnsToArchive()
    {
        await OpenFirstInstanceAsync();

        var archive = _page.Locator(".app-topbar button:has-text('Archive')");
        await archive.ClickAsync();

        var dialog = _page.GetByRole(AriaRole.Dialog, new() { Name = "Confirm archive" });
        await Assertions.Expect(dialog).ToBeVisibleAsync();
        Assert.That(await _page.Locator("dialog[data-testid=archive-dialog]").EvaluateAsync<bool>("d => d.matches(':modal')"), Is.True,
            "Opened with showModal(), so the page behind is inert and focus stays inside.");

        // The preview and the two archive kinds are inside the dialog; the safe action has focus first.
        await Assertions.Expect(dialog.Locator("ul li").First).ToBeVisibleAsync();
        await Assertions.Expect(dialog.Locator("[data-testid=archive-kind-mistake]")).ToBeVisibleAsync();
        await Assertions.Expect(dialog.Locator("button:has-text('Cancel')")).ToBeFocusedAsync();

        // A reason is still required before the archive button enables.
        await dialog.Locator("[data-testid=archive-kind-mistake]").ClickAsync();
        var confirm = dialog.Locator("button:has-text('Archive this instance')");
        await Assertions.Expect(confirm).ToBeDisabledAsync();

        await _page.Keyboard.PressAsync("Escape");
        await Assertions.Expect(dialog).Not.ToBeVisibleAsync();
        await Assertions.Expect(archive).ToBeFocusedAsync();
    }

    [Test]
    public async Task ArchiveConfirm_FitsA360pxPhone()
    {
        await _page.SetViewportSizeAsync(360, 740);
        await OpenFirstInstanceAsync();
        await _page.Locator(".app-topbar button:has-text('Archive')").ClickAsync();

        var dialog = _page.Locator("dialog[data-testid=archive-dialog]");
        await Assertions.Expect(dialog).ToBeVisibleAsync();
        var box = await dialog.BoundingBoxAsync();
        Assert.That(box, Is.Not.Null);
        Assert.That(box!.X, Is.GreaterThanOrEqualTo(0));
        Assert.That(box.X + box.Width, Is.LessThanOrEqualTo(360));
        Assert.That(await _page.EvaluateAsync<bool>("() => document.documentElement.scrollWidth <= 360"), Is.True, "No sideways scroll.");
    }

    [Test]
    public async Task NotificationMenu_IsAPopover_LightDismissesOnOutsideClick()
    {
        await GotoAsync("/");
        await WaitForCircuitAsync();

        var bell = _page.Locator("button.notif-bell-btn");
        var menu = _page.Locator("#notif-dropdown");
        await bell.ClickAsync();
        await Assertions.Expect(menu).ToBeVisibleAsync();
        Assert.That(await menu.EvaluateAsync<bool>("m => m.matches(':popover-open')"), Is.True);
        await Assertions.Expect(bell).ToHaveAttributeAsync("aria-expanded", "true");

        // Clicking elsewhere closes it (light dismiss), and the bell's expanded state follows.
        await _page.Locator(".app-topbar h1").ClickAsync();
        await Assertions.Expect(menu).Not.ToBeVisibleAsync();
        await Assertions.Expect(bell).ToHaveAttributeAsync("aria-expanded", "false");
    }

    [Test]
    public async Task NotificationMenu_KeyboardOpen_TabEntersIt_EscapeReturnsFocusToTheBell()
    {
        await GotoAsync("/");
        await WaitForCircuitAsync();

        var bell = _page.Locator("button.notif-bell-btn");
        await bell.FocusAsync();
        await _page.Keyboard.PressAsync("Enter");
        var menu = _page.Locator("[role=region][aria-label='Recent notifications']");
        await Assertions.Expect(menu).ToBeVisibleAsync();
        await Assertions.Expect(menu.Locator("[data-testid=notif-updated]")).ToBeVisibleAsync();

        await _page.Keyboard.PressAsync("Tab");
        Assert.That(await menu.EvaluateAsync<bool>("m => m.contains(document.activeElement)"), Is.True,
            "Tab from the bell moves into the menu, which sits right after it in the DOM.");

        await _page.Keyboard.PressAsync("Escape");
        await Assertions.Expect(menu).Not.ToBeVisibleAsync();
        await Assertions.Expect(bell).ToBeFocusedAsync();
    }

    [Test]
    public async Task SectionHelp_IsAToggletip_OpensOnClick_EscapeCloses()
    {
        await OpenFirstInstanceAsync();

        var trigger = _page.Locator("[data-testid=instance-sections-help]");
        await Assertions.Expect(trigger).ToHaveAttributeAsync("aria-label", "What each instance section covers");
        await Assertions.Expect(trigger).ToHaveAttributeAsync("aria-expanded", "false");

        await trigger.ClickAsync();
        var tip = _page.Locator("[role=tooltip]:has-text('Recovery objectives, backup-chain coverage')");
        await Assertions.Expect(tip).ToBeVisibleAsync();
        await Assertions.Expect(trigger).ToHaveAttributeAsync("aria-expanded", "true");

        // Placed next to its button, not centered in the window.
        var tipBox = await tip.BoundingBoxAsync();
        var triggerBox = await trigger.BoundingBoxAsync();
        var gapBelow = Math.Abs(tipBox!.Y - (triggerBox!.Y + triggerBox.Height));
        var gapAbove = Math.Abs(triggerBox.Y - (tipBox.Y + tipBox.Height));
        Assert.That(Math.Min(gapBelow, gapAbove), Is.LessThan(24), "The tip opens just below (or above) its button.");

        await _page.Keyboard.PressAsync("Escape");
        await Assertions.Expect(tip).Not.ToBeVisibleAsync();
        await Assertions.Expect(trigger).ToHaveAttributeAsync("aria-expanded", "false");

        // The section links no longer rely on hover-only titles.
        Assert.That(await _page.Locator("nav[aria-label='Instance sections'] a[title]").CountAsync(), Is.Zero);
    }

    [Test]
    public async Task StatStrip_ReflowsByItsOwnWidth_AtTwoHundredPercentZoom()
    {
        // 640px is a 1280px window at 200%: above the 480px media query, so only the container query applies.
        await _page.SetViewportSizeAsync(640, 900);
        await GotoAsync("/");
        await _page.WaitForSelectorAsync(".stat-strip .stat-cell");
        await WaitForCircuitAsync();

        var cells = _page.Locator(".stat-strip .stat-cell");
        var strip = await _page.Locator(".stat-strip").BoundingBoxAsync();
        var total = await cells.Nth(0).BoundingBoxAsync();
        var second = await cells.Nth(1).BoundingBoxAsync();
        var third = await cells.Nth(2).BoundingBoxAsync();

        Assert.That(total!.Width, Is.EqualTo(strip!.Width).Within(2), "Total spans the strip.");
        Assert.That(second!.Y, Is.GreaterThan(total.Y), "The health counts start on the next row.");
        Assert.That(third!.Y, Is.EqualTo(second.Y).Within(2), "They pair up two per row.");
        Assert.That(await _page.EvaluateAsync<bool>("() => document.documentElement.scrollWidth <= 640"), Is.True);
    }

    [Test]
    public async Task DetailCards_StackWhenTheirRowIsNarrow_NotOnlyWhenTheWindowIs()
    {
        // 800px is above Bootstrap's md breakpoint and the old 767px media query, so the two cards would
        // sit side by side at about 340px each; the row's container query stacks them.
        await _page.SetViewportSizeAsync(800, 900);
        await OpenFirstInstanceAsync();

        var row = _page.Locator(".kv-row").First;
        var first = await row.Locator(":scope > [class*='col-']").Nth(0).BoundingBoxAsync();
        var second = await row.Locator(":scope > [class*='col-']").Nth(1).BoundingBoxAsync();
        Assert.That(second!.Y, Is.GreaterThanOrEqualTo(first!.Y + first.Height - 1), "The two detail cards stack instead of squeezing side by side.");
        Assert.That(await _page.EvaluateAsync<bool>("() => document.documentElement.scrollWidth <= 800"), Is.True);
    }

    private async Task OpenFirstInstanceAsync()
    {
        await GotoAsync("/instances");
        await _page.WaitForSelectorAsync(".dbt-table tbody tr");
        await WaitForCircuitAsync();
        await _page.ClickAsync(".dbt-table tbody tr");
        await _page.WaitForURLAsync(url => System.Text.RegularExpressions.Regex.IsMatch(url, @"/instances/\d+"));
        await WaitForCircuitAsync();
    }

    private Task LoginAsync(string email) => LoginAsync(email, "ArxDemo#2026!");

    private async Task LoginAsync(string email, string password)
    {
        await GotoAsync("/Account/Login");
        await _page.FillAsync("input[autocomplete~=username]", email);
        await _page.FillAsync("input[autocomplete=current-password]", password);
        await _page.ClickAsync("button[type=submit]");
        await _page.WaitForURLAsync(url => !url.Contains("/Account/Login"), new PageWaitForURLOptions { Timeout = 15000 });
        await WaitForCircuitAsync();
    }

    private Task GotoAsync(string path) =>
        _page.GotoAsync(TestServerFixture.BaseUrl + path, new PageGotoOptions { WaitUntil = WaitUntilState.Load });

    private Task WaitForCircuitAsync() => _page.WaitForTimeoutAsync(800);
}
