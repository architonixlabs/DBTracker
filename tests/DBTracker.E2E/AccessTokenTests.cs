using System.Net;
using System.Net.Http.Headers;
using System.Text.Json;
using Microsoft.Playwright;

namespace DBTracker.E2E;

/// <summary>I06: personal access tokens on /account/tokens, the bearer-only /api/tray/summary, and anonymous /healthz.</summary>
[TestFixture]
public class AccessTokenTests
{
    private static readonly HttpClient Http = new(new HttpClientHandler { AllowAutoRedirect = false })
    {
        BaseAddress = new Uri(TestServerFixture.BaseUrl),
    };

    private IPage _page = null!;

    [TearDown]
    public async Task TearDown()
    {
        if (_page is not null)
        {
            await _page.CloseAsync();
        }
    }

    [Test]
    public async Task Healthz_IsAnonymous_AndReportsOk()
    {
        using var response = await Http.GetAsync("/healthz");

        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.OK));
        using var json = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        Assert.That(json.RootElement.GetProperty("status").GetString(), Is.EqualTo("ok"));
        Assert.That(json.RootElement.GetProperty("installed").GetBoolean(), Is.True);
        Assert.That(json.RootElement.GetProperty("database").GetString(), Is.EqualTo("ok"));
        Assert.That(json.RootElement.GetProperty("version").GetString(), Is.Not.Empty);
        Assert.That(json.RootElement.EnumerateObject().Select(p => p.Name), Is.EquivalentTo(new[] { "status", "installed", "database", "version" }));
    }

    [Test]
    public async Task Summary_WithoutAValidToken_Is401_AndNeverARedirect()
    {
        using var noToken = await Http.GetAsync("/api/tray/summary");
        Assert.That(noToken.StatusCode, Is.EqualTo(HttpStatusCode.Unauthorized));
        Assert.That(noToken.Headers.Location, Is.Null);

        using var badToken = await GetSummaryAsync("dbt_" + new string('A', 43));
        Assert.That(badToken.StatusCode, Is.EqualTo(HttpStatusCode.Unauthorized));
        Assert.That(badToken.Headers.Location, Is.Null);

        using var garbage = await GetSummaryAsync("not-a-token");
        Assert.That(garbage.StatusCode, Is.EqualTo(HttpStatusCode.Unauthorized));
    }

    [Test]
    public async Task CreatedToken_IsShownOnce_ReturnsTheSummary_AndStopsWorkingWhenRevoked()
    {
        _page = await GlobalSetup.Server.Browser.NewPageAsync();
        await LoginAsync(_page, "priya.nair@architonixlabs.com", "ArxDemo#2026!");

        var tokenName = $"E2E tray {Guid.NewGuid():N}"[..20];
        var token = await CreateTokenAsync(_page, tokenName);
        Assert.That(token, Does.StartWith("dbt_"));

        using (var response = await GetSummaryAsync(token))
        {
            Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.OK));
            using var json = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
            var root = json.RootElement;
            Assert.That(root.GetProperty("user").GetString(), Is.EqualTo("priya.nair@architonixlabs.com"));
            Assert.That(root.GetProperty("dashboardUrl").GetString(), Is.EqualTo("/"));
            var failed = root.GetProperty("failed").GetInt32();
            var overdue = root.GetProperty("overdue").GetInt32();
            var never = root.GetProperty("neverBackedUp").GetInt32();
            var atRisk = root.GetProperty("atRisk");
            // Seed data always has at-risk instances; the list is worst-first and never larger than the counts.
            Assert.That(failed + overdue + never, Is.GreaterThan(0));
            Assert.That(atRisk.GetArrayLength(), Is.EqualTo(Math.Min(20, failed + overdue + never)));
            var worstExpected = "Overdue";
            if (never > 0) worstExpected = "NeverBackedUp";
            if (failed > 0) worstExpected = "Failed";
            Assert.That(atRisk[0].GetProperty("health").GetString(), Is.EqualTo(worstExpected));
            Assert.That(atRisk[0].TryGetProperty("environment", out _), Is.True);
            Assert.That(atRisk[0].TryGetProperty("lastSuccessUtc", out _), Is.True);
        }

        // Shown once: a fresh visit lists the token but never shows its value again.
        await GotoAsync(_page, "/account/tokens");
        await _page.WaitForSelectorAsync("table[aria-label='Your access tokens']");
        Assert.That(await _page.Locator("[data-testid=new-token-panel]").CountAsync(), Is.Zero);
        Assert.That(await _page.ContentAsync(), Does.Not.Contain(token));
        var row = _page.Locator("tr", new PageLocatorOptions { HasText = tokenName });
        await Assertions.Expect(row).ToContainTextAsync("Active");

        await WaitForCircuitAsync(_page);
        await row.Locator("button:has-text('Revoke')").ClickAsync();
        await Assertions.Expect(row).ToContainTextAsync("Revoked");

        using var afterRevoke = await GetSummaryAsync(token);
        Assert.That(afterRevoke.StatusCode, Is.EqualTo(HttpStatusCode.Unauthorized));
    }

    [Test]
    public async Task DeactivatingTheUser_MakesTheirTokenFailImmediately()
    {
        var admin = await GlobalSetup.Server.Browser.NewPageAsync();
        try
        {
            await LoginAsync(admin, "admin@architonixlabs.com", "ArxDemo#2026!");
            await GotoAsync(admin, "/users");
            await admin.WaitForSelectorAsync("button[type=submit]");
            await WaitForCircuitAsync(admin);
            var email = $"e2e-token-{Guid.NewGuid():N}@architonixlabs.com";
            await FillByLabel(admin, "Display name", "E2E Token User");
            await FillByLabel(admin, "Email", email);
            await FillByLabel(admin, "Temporary password", "Str0ngTempPass!");
            await admin.Locator("label:text-is('Role') ~ select, label:text-is('Role') + select").First
                .SelectOptionAsync(new SelectOptionValue { Label = "Viewer" });
            await admin.ClickAsync("button[type=submit]:text('Create user')");
            await Assertions.Expect(admin.Locator("tr", new PageLocatorOptions { HasText = email })).ToBeVisibleAsync();

            // The new user signs in, sets their own password, then creates a token.
            _page = await GlobalSetup.Server.Browser.NewPageAsync();
            await LoginAsync(_page, email, "Str0ngTempPass!");
            await _page.WaitForURLAsync(url => url.Contains("/account/change-password"));
            await WaitForCircuitAsync(_page);
            await FillByLabel(_page, "Current password", "Str0ngTempPass!");
            await FillByLabel(_page, "New password", "N3wOwnPassword!");
            // Not a bare button[type=submit]: the sidebar's Log out button is one too.
            await _page.ClickAsync("button[type=submit]:text('Change password')");
            await Assertions.Expect(_page.Locator(".dbt-toast")).ToContainTextAsync("Password changed");

            var token = await CreateTokenAsync(_page, "E2E deactivation");
            using (var before = await GetSummaryAsync(token))
            {
                Assert.That(before.StatusCode, Is.EqualTo(HttpStatusCode.OK));
            }

            var userRow = admin.Locator("tr", new PageLocatorOptions { HasText = email });
            await userRow.Locator("button:has-text('Deactivate')").ClickAsync();
            await Assertions.Expect(userRow).ToContainTextAsync("Deactivated");

            using var after = await GetSummaryAsync(token);
            Assert.That(after.StatusCode, Is.EqualTo(HttpStatusCode.Unauthorized));
        }
        finally
        {
            await admin.CloseAsync();
        }
    }

    private static async Task<string> CreateTokenAsync(IPage page, string name)
    {
        await GotoAsync(page, "/account/tokens");
        await page.WaitForSelectorAsync("#token-name");
        await WaitForCircuitAsync(page);
        await page.FillAsync("#token-name", name);
        await page.ClickAsync("button[type=submit]:text('Create token')");
        var value = page.Locator("#new-token-value");
        await Assertions.Expect(value).ToBeVisibleAsync();
        await Assertions.Expect(page.Locator("[data-testid=new-token-panel]")).ToContainTextAsync("won't see it again");
        return await value.InputValueAsync();
    }

    private static async Task<HttpResponseMessage> GetSummaryAsync(string token)
    {
        using var request = new HttpRequestMessage(HttpMethod.Get, "/api/tray/summary");
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        return await Http.SendAsync(request);
    }

    private static async Task LoginAsync(IPage page, string email, string password)
    {
        await GotoAsync(page, "/Account/Login");
        await page.FillAsync("input[autocomplete~=username]", email);
        await page.FillAsync("input[autocomplete=current-password]", password);
        await page.ClickAsync("button[type=submit]");
        await page.WaitForURLAsync(url => !url.Contains("/Account/Login"), new PageWaitForURLOptions { Timeout = 15000 });
        await WaitForCircuitAsync(page);
    }

    private static Task GotoAsync(IPage page, string path) =>
        page.GotoAsync(TestServerFixture.BaseUrl + path, new PageGotoOptions { WaitUntil = WaitUntilState.Load });

    private static Task WaitForCircuitAsync(IPage page) => page.WaitForTimeoutAsync(800);

    private static Task FillByLabel(IPage page, string label, string value) =>
        page.Locator($"label:text-is('{label}') ~ input, label:text-is('{label}') + input").First.FillAsync(value);
}
