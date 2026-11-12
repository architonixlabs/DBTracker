using Microsoft.Playwright;

namespace DBTracker.E2E;

/// <summary>Design polish (UPDATE-034): the System / Dark / Light theme switch, applied from the dbt-theme cookie before
/// first paint on every page including sign-in, and the shared EmptyState component.</summary>
[TestFixture]
public class ThemeAndEmptyStateTests
{
    private const string Password = "ArxDemo#2026!";
    private const string DarkBg = "rgb(11, 13, 16)";
    private const string LightBg = "rgb(246, 247, 249)";

    private IBrowserContext? _context;
    private IPage _page = null!;
    private readonly List<string> _cspErrors = [];

    [TearDown]
    public async Task TearDown()
    {
        await _page.CloseAsync();
        if (_context is not null)
        {
            await _context.CloseAsync();
            _context = null;
        }
    }

    [Test]
    public async Task ThemeChoice_AppliesAtOnce_SurvivesReloadAndSignOut_WithNoCspErrors()
    {
        await OpenPageAsync(new BrowserNewContextOptions());
        await LoginAsync("priya.nair@architonixlabs.com");

        // Dark is the default with no cookie (Priya's console look).
        Assert.That(await ThemeAttributeAsync(), Is.EqualTo("dark"));
        Assert.That(await BodyBackgroundAsync(), Is.EqualTo(DarkBg));
        await Assertions.Expect(_page.Locator("[data-testid=theme-dark]")).ToBeCheckedAsync();
        await Assertions.Expect(_page.Locator("[data-testid=theme-switch] legend")).ToHaveTextAsync("Theme");

        await _page.Locator("[data-testid=theme-light]").CheckAsync();
        Assert.That(await ThemeAttributeAsync(), Is.EqualTo("light"));
        Assert.That(await BodyBackgroundAsync(), Is.EqualTo(LightBg));

        // Keyboard: the choices are one native radio group, so arrow keys move the selection.
        await _page.Locator("[data-testid=theme-light]").FocusAsync();
        await _page.Keyboard.PressAsync("ArrowLeft");
        await Assertions.Expect(_page.Locator("[data-testid=theme-dark]")).ToBeCheckedAsync();
        Assert.That(await ThemeAttributeAsync(), Is.EqualTo("dark"));
        await _page.Keyboard.PressAsync("ArrowRight");
        await Assertions.Expect(_page.Locator("[data-testid=theme-light]")).ToBeCheckedAsync();
        Assert.That(await ThemeAttributeAsync(), Is.EqualTo("light"));

        // Reload: the server reads the cookie and renders <html data-theme="light"> itself.
        var reload = await _page.ReloadAsync(new PageReloadOptions { WaitUntil = WaitUntilState.Load });
        Assert.That(await reload!.TextAsync(), Does.Contain("data-theme=\"light\""), "the first paint must already be light");
        await WaitForCircuitAsync();
        await Assertions.Expect(_page.Locator("[data-testid=theme-light]")).ToBeCheckedAsync();
        Assert.That(await BodyBackgroundAsync(), Is.EqualTo(LightBg));

        // Sign-out clears site data, cookies included; the sign-in page still opens in the chosen theme.
        await _page.ClickAsync("form[action*='Logout'] button[type=submit]");
        await _page.WaitForURLAsync(url => url.Contains("/Account/Login"), new PageWaitForURLOptions { Timeout = 15000 });
        Assert.That(await ThemeAttributeAsync(), Is.EqualTo("light"));
        Assert.That(await BodyBackgroundAsync(), Is.EqualTo(LightBg));

        // And a fresh visit to the static sign-in page reads the re-issued cookie.
        var login = await GotoAsync("/Account/Login");
        Assert.That(await login!.TextAsync(), Does.Contain("data-theme=\"light\""));
        Assert.That(await BodyBackgroundAsync(), Is.EqualTo(LightBg));

        Assert.That(_cspErrors, Is.Empty, "CSP blocked something:\n" + string.Join("\n", _cspErrors));
    }

    [TestCase(ColorScheme.Light, LightBg)]
    [TestCase(ColorScheme.Dark, DarkBg)]
    public async Task ThemeSystem_FollowsTheDeviceSetting_OnTheSignInPage(ColorScheme scheme, string expectedBg)
    {
        await OpenPageAsync(new BrowserNewContextOptions { ColorScheme = scheme });
        await _context!.AddCookiesAsync([new Cookie { Name = "dbt-theme", Value = "system", Url = TestServerFixture.BaseUrl }]);

        await GotoAsync("/Account/Login");
        Assert.That(await ThemeAttributeAsync(), Is.EqualTo("system"));
        Assert.That(await BodyBackgroundAsync(), Is.EqualTo(expectedBg));
        Assert.That(_cspErrors, Is.Empty, string.Join("\n", _cspErrors));
    }

    [Test]
    public async Task ThemeSwitch_OnA360pxPhone_Is44pxIconTargets_AndTheLightPageDoesNotScrollSideways()
    {
        await OpenPageAsync(new BrowserNewContextOptions { ViewportSize = new ViewportSize { Width = 360, Height = 740 } });
        await LoginAsync("priya.nair@architonixlabs.com");

        var light = _page.Locator("label.theme-option:has([data-testid=theme-light])");
        var box = await light.BoundingBoxAsync();
        Assert.That(box, Is.Not.Null);
        Assert.That(box!.Width, Is.GreaterThanOrEqualTo(44));
        Assert.That(box.Height, Is.GreaterThanOrEqualTo(44));

        await _page.Locator("[data-testid=theme-light]").CheckAsync();
        Assert.That(await BodyBackgroundAsync(), Is.EqualTo(LightBg));
        var overflow = await _page.EvaluateAsync<int>("() => document.documentElement.scrollWidth - document.documentElement.clientWidth");
        Assert.That(overflow, Is.LessThanOrEqualTo(0), "the page scrolls sideways at 360px");
    }

    [Test]
    public async Task EmptyState_ShowsWhyAndTheNextAction_OnInstancesAndArchive()
    {
        await OpenPageAsync(new BrowserNewContextOptions());
        await LoginAsync("priya.nair@architonixlabs.com");

        // Instances filtered to nothing: says why, offers Clear filters, and clearing brings rows back.
        await GotoAsync("/instances?s=zz-no-such-instance-zz");
        await WaitForCircuitAsync();
        var empty = _page.Locator("[data-testid=empty-state]");
        await Assertions.Expect(empty).ToBeVisibleAsync();
        await Assertions.Expect(empty).ToContainTextAsync("No instances match these filters");
        await Assertions.Expect(empty).ToHaveAttributeAsync("role", "status");
        await empty.GetByRole(AriaRole.Button, new LocatorGetByRoleOptions { Name = "Clear filters" }).ClickAsync();
        await Assertions.Expect(empty).ToHaveCountAsync(0);
        await Assertions.Expect(_page.Locator(".dbt-table tbody tr").First).ToBeVisibleAsync();

        // Archived instances searched to nothing.
        await GotoAsync("/instances/archived");
        await WaitForCircuitAsync();
        await _page.FillAsync("#archived-search", "zz-no-such-instance-zz");
        var archivedEmpty = _page.Locator("[data-testid=empty-state]");
        await Assertions.Expect(archivedEmpty).ToContainTextAsync("No archived instances match this search");

        // The same component renders in the light theme with readable muted text (not the dark-theme grey).
        await _page.Locator("[data-testid=theme-light]").CheckAsync();
        var color = await archivedEmpty.Locator(".empty-state-message").EvaluateAsync<string>("el => getComputedStyle(el).color");
        Assert.That(color, Is.EqualTo("rgb(89, 97, 108)"));

        Assert.That(_cspErrors, Is.Empty, string.Join("\n", _cspErrors));
    }

    private async Task OpenPageAsync(BrowserNewContextOptions options)
    {
        _context = await GlobalSetup.Server.Browser.NewContextAsync(options);
        _page = await _context.NewPageAsync();
        _page.Console += (_, msg) =>
        {
            if (msg.Text.Contains("Content Security Policy", StringComparison.OrdinalIgnoreCase) &&
                !msg.Text.Contains("report-only", StringComparison.OrdinalIgnoreCase) &&
                !msg.Text.Contains("Trusted Type", StringComparison.OrdinalIgnoreCase))
            {
                _cspErrors.Add($"{_page.Url}: {msg.Text}");
            }
        };
    }

    private Task<string?> ThemeAttributeAsync() => _page.Locator("html").GetAttributeAsync("data-theme");

    private Task<string> BodyBackgroundAsync() => _page.EvaluateAsync<string>("() => getComputedStyle(document.body).backgroundColor");

    private async Task LoginAsync(string email)
    {
        await GotoAsync("/Account/Login");
        await _page.FillAsync("input[autocomplete~=username]", email);
        await _page.FillAsync("input[autocomplete=current-password]", Password);
        await _page.ClickAsync("button[type=submit]");
        await _page.WaitForURLAsync(url => !url.Contains("/Account/Login"), new PageWaitForURLOptions { Timeout = 15000 });
        await WaitForCircuitAsync();
    }

    private Task<IResponse?> GotoAsync(string path) =>
        _page.GotoAsync(TestServerFixture.BaseUrl + path, new PageGotoOptions { WaitUntil = WaitUntilState.Load });

    private Task WaitForCircuitAsync() => _page.WaitForTimeoutAsync(800);
}
