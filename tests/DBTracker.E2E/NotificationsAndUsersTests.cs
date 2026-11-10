using Microsoft.Playwright;

namespace DBTracker.E2E;

[TestFixture]
public class NotificationsAndUsersTests
{
    private IPage _page = null!;

    [TearDown]
    public async Task TearDown()
    {
        await _page.CloseAsync();
    }

    [Test]
    public async Task LogOut_ReturnsToLoginPage_AndEndsTheSession()
    {
        _page = await GlobalSetup.Server.Browser.NewPageAsync();
        await LoginAsync("priya.nair@architonixlabs.com");

        await GotoAsync("/");
        await _page.WaitForSelectorAsync(".user-chip");
        await WaitForCircuitAsync();

        await _page.ClickAsync("form[action='Account/Logout'] button");
        await _page.WaitForURLAsync(url => url.Contains("/Account/Login"));

        // Session must actually be gone — a protected page should bounce back to login, not just the chrome.
        await GotoAsync("/instances");
        await _page.WaitForURLAsync(url => url.Contains("/Account/Login"));
    }

    [Test]
    public async Task NotificationBell_ShowsUnreadSeedData_AndMarkAllReadClearsBadge()
    {
        _page = await GlobalSetup.Server.Browser.NewPageAsync();
        await LoginAsync("priya.nair@architonixlabs.com");

        await GotoAsync("/");
        await _page.WaitForSelectorAsync(".notif-bell-btn");
        await WaitForCircuitAsync();

        // Seed data has 2 unread (Failed) + 1 read (Overdue) notification.
        await Assertions.Expect(_page.Locator(".notif-badge")).ToHaveTextAsync("2");

        await _page.ClickAsync(".notif-bell-btn");
        await _page.WaitForTimeoutAsync(400);

        await Assertions.Expect(_page.Locator(".notif-item")).ToHaveCountAsync(3);
        await Assertions.Expect(_page.Locator(".notif-item.unread")).ToHaveCountAsync(2);

        await _page.ClickAsync(".notif-mark-read");
        await _page.WaitForTimeoutAsync(400);

        await Assertions.Expect(_page.Locator(".notif-badge")).Not.ToBeVisibleAsync();
        await Assertions.Expect(_page.Locator(".notif-item.unread")).ToHaveCountAsync(0);
    }

    [Test]
    public async Task AdminCanCreateUser_AndNonAdminIsBlockedFromUsersPage()
    {
        _page = await GlobalSetup.Server.Browser.NewPageAsync();
        await LoginAsync("admin@architonixlabs.com");

        await GotoAsync("/users");
        await _page.WaitForSelectorAsync("button[type=submit]");
        await WaitForCircuitAsync();

        var email = $"e2e-user-{Guid.NewGuid():N}@architonixlabs.com";
        await FillByLabel("Display name", "E2E Test User");
        await FillByLabel("Email", email);
        await FillByLabel("Temporary password", "Str0ngTempPass!");
        await SelectByLabel("Role", "Auditor");
        await _page.ClickAsync("button[type=submit]:text('Create user')");
        await _page.WaitForTimeoutAsync(800);

        await Assertions.Expect(_page.Locator("table tbody", new PageLocatorOptions { HasText = email })).ToBeVisibleAsync();

        // DBA (not Admin) should not be able to reach /users at all.
        await LoginAsync("priya.nair@architonixlabs.com");
        await GotoAsync("/users");
        await _page.WaitForURLAsync(url => url.Contains("/Account/AccessDenied"));
    }

    [Test]
    public async Task SendReport_WithDistributionListConfigured_ShowsSuccessToast()
    {
        _page = await GlobalSetup.Server.Browser.NewPageAsync();
        // Auditor is in Roles.ReportingPolicy and read-only otherwise — proves the button isn't DBA-gated.
        await LoginAsync("angela.price@contoso.com");

        await GotoAsync("/compliance");
        await _page.WaitForSelectorAsync(".dbt-table table");
        await WaitForCircuitAsync();

        await _page.ClickAsync("button:has-text('Send report')");
        await _page.WaitForTimeoutAsync(1000);

        await Assertions.Expect(_page.Locator(".dbt-toast")).ToContainTextAsync("Report sent to 1 recipient");
    }

    private async Task LoginAsync(string email)
    {
        await GotoAsync("/Account/Login");
        await _page.FillAsync("input[autocomplete=username]", email);
        await _page.FillAsync("input[autocomplete=current-password]", "ArxDemo#2026!");

        await _page.RunAndWaitForNavigationAsync(
            () => _page.ClickAsync("button[type=submit]"),
            new PageRunAndWaitForNavigationOptions { WaitUntil = WaitUntilState.NetworkIdle, Timeout = 15000 });

        await WaitForCircuitAsync();
    }

    private Task GotoAsync(string path) =>
        _page.GotoAsync(TestServerFixture.BaseUrl + path, new PageGotoOptions { WaitUntil = WaitUntilState.NetworkIdle });

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
