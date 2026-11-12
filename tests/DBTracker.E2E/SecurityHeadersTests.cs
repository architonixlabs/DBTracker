using Microsoft.Playwright;

namespace DBTracker.E2E;

/// <summary>G25/G27: every page carries the CSP and companion headers, nothing the app ships is blocked by them,
/// and no page requests a third-party font or script.</summary>
[TestFixture]
public class SecurityHeadersTests
{
    private IPage _page = null!;

    [TearDown]
    public async Task TearDown() => await _page.CloseAsync();

    [Test]
    public async Task MainPages_SendTheHeaders_HaveNoCspViolations_AndLoadNothingThirdParty()
    {
        _page = await GlobalSetup.Server.Browser.NewPageAsync();
        var violations = new List<string>();
        var thirdParty = new List<string>();
        _page.Console += (_, msg) =>
        {
            if (msg.Text.Contains("Content Security Policy", StringComparison.OrdinalIgnoreCase) &&
                !msg.Text.Contains("report-only", StringComparison.OrdinalIgnoreCase) &&
                !msg.Text.Contains("Trusted Type", StringComparison.OrdinalIgnoreCase))
            {
                violations.Add($"{_page.Url}: {msg.Text}");
            }
        };
        _page.Request += (_, req) =>
        {
            if (!req.Url.StartsWith(TestServerFixture.BaseUrl, StringComparison.OrdinalIgnoreCase) && !req.Url.StartsWith("data:"))
            {
                thirdParty.Add(req.Url);
            }
        };

        var login = await _page.GotoAsync(TestServerFixture.BaseUrl + "/Account/Login", new PageGotoOptions { WaitUntil = WaitUntilState.Load });
        var headers = login!.Headers;
        Assert.That(headers["content-security-policy"], Does.Contain("'strict-dynamic'"));
        Assert.That(headers["content-security-policy"], Does.Contain("frame-ancestors 'none'"));
        Assert.That(headers["x-content-type-options"], Is.EqualTo("nosniff"));
        Assert.That(headers["referrer-policy"], Is.EqualTo("strict-origin-when-cross-origin"));
        Assert.That(headers["x-frame-options"], Is.EqualTo("DENY"));

        await _page.FillAsync("input[autocomplete~=username]", "admin@architonixlabs.com");
        await _page.FillAsync("input[autocomplete=current-password]", "ArxDemo#2026!");
        await _page.ClickAsync("button[type=submit]");
        await _page.WaitForURLAsync(url => !url.Contains("/Account/Login"), new PageWaitForURLOptions { Timeout = 15000 });

        foreach (var path in new[] { "/", "/instances", "/reports", "/compliance", "/notifications", "/audit", "/setup", "/lifecycle", "/instances/import" })
        {
            await _page.GotoAsync(TestServerFixture.BaseUrl + path, new PageGotoOptions { WaitUntil = WaitUntilState.Load });
            await _page.WaitForTimeoutAsync(900);
        }

        // Interactivity still works under the CSP: the circuit connected and a click reached the server.
        await _page.GotoAsync(TestServerFixture.BaseUrl + "/", new PageGotoOptions { WaitUntil = WaitUntilState.Load });
        await _page.WaitForSelectorAsync(".notif-bell-btn");
        await _page.WaitForTimeoutAsync(900);
        await _page.ClickAsync(".notif-bell-btn");
        await Assertions.Expect(_page.Locator("#notif-dropdown")).ToBeVisibleAsync();

        Assert.That(violations, Is.Empty, "CSP blocked something the app ships:\n" + string.Join("\n", violations));
        Assert.That(thirdParty, Is.Empty, "Pages requested third-party resources:\n" + string.Join("\n", thirdParty));
    }

    [Test]
    public async Task Logout_ClearsSiteData()
    {
        _page = await GlobalSetup.Server.Browser.NewPageAsync();
        await _page.GotoAsync(TestServerFixture.BaseUrl + "/Account/Login", new PageGotoOptions { WaitUntil = WaitUntilState.Load });
        await _page.FillAsync("input[autocomplete~=username]", "dev.patel@ediqra.com");
        await _page.FillAsync("input[autocomplete=current-password]", "ArxDemo#2026!");
        await _page.ClickAsync("button[type=submit]");
        await _page.WaitForURLAsync(url => !url.Contains("/Account/Login"), new PageWaitForURLOptions { Timeout = 15000 });
        await _page.WaitForTimeoutAsync(900);

        var logoutResponse = _page.WaitForResponseAsync(r => r.Request.Method == "POST" && new Uri(r.Url).AbsolutePath.EndsWith("/Account/Logout"));
        await _page.ClickAsync("form[action*='Logout'] button[type=submit]");
        var response = await logoutResponse;
        // Headers omits ones the browser acts on itself (Set-Cookie, Clear-Site-Data); AllHeadersAsync returns the raw set.
        var all = await response.AllHeadersAsync();
        Assert.That(all.TryGetValue("clear-site-data", out var value) ? value : "", Does.Contain("\"cookies\""));
    }
}
