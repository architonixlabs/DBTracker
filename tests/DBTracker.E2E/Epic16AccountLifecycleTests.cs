using Microsoft.Playwright;

namespace DBTracker.E2E;

/// <summary>E2E coverage for Epic 16's U16 (account lifecycle: deactivate/reactivate, last-Admin guard, password reset link) and a cross-cutting U19 audit-view check.</summary>
[TestFixture]
public class Epic16AccountLifecycleTests
{
    private IPage _page = null!;

    [TearDown]
    public async Task TearDown() => await _page.CloseAsync();

    [Test]
    public async Task DeactivatingTheLastAdmin_IsBlocked_ButDeactivatingAnOrdinaryUser_Succeeds_AndLocksThemOut()
    {
        _page = await GlobalSetup.Server.Browser.NewPageAsync();
        await LoginAsync("admin@architonixlabs.com");

        // Create a disposable Viewer account to deactivate/reactivate safely.
        await GotoAsync("/users");
        await _page.WaitForSelectorAsync("button[type=submit]");
        await WaitForCircuitAsync();
        var email = $"e2e-lifecycle-{Guid.NewGuid():N}@architonixlabs.com";
        await FillByLabel("Display name", "E2E Lifecycle User");
        await FillByLabel("Email", email);
        await FillByLabel("Temporary password", "Str0ngTempPass!");
        await SelectByLabel("Role", "Viewer");
        await _page.ClickAsync("button[type=submit]:text('Create user')");
        await _page.WaitForTimeoutAsync(800);

        var row = _page.Locator("tr", new PageLocatorOptions { HasText = email });
        await row.Locator("button:has-text('Deactivate')").ClickAsync();
        await _page.WaitForTimeoutAsync(600);
        await Assertions.Expect(row).ToContainTextAsync("Deactivated");

        // The deactivated account can no longer sign in at all.
        await GotoAsync("/Account/Login");
        await _page.FillAsync("input[autocomplete~=username]", email);
        await _page.FillAsync("input[autocomplete=current-password]", "Str0ngTempPass!");
        await _page.ClickAsync("button[type=submit]");
        await _page.WaitForTimeoutAsync(600);
        await Assertions.Expect(_page.Locator("text=deactivated")).ToBeVisibleAsync();

        // Reactivating restores access.
        await LoginAsync("admin@architonixlabs.com");
        await GotoAsync("/users");
        await _page.WaitForSelectorAsync("button[type=submit]");
        await WaitForCircuitAsync();
        var rowAfter = _page.Locator("tr", new PageLocatorOptions { HasText = email });
        await rowAfter.Locator("button:has-text('Reactivate')").ClickAsync();
        await _page.WaitForTimeoutAsync(600);
        await Assertions.Expect(rowAfter).ToContainTextAsync("Active");

        // The last-Admin guard: deactivating the only Admin account must be rejected, not silently
        // allowed (which would lock everyone out of /users and /setup).
        var adminRow = _page.Locator("tr", new PageLocatorOptions { HasText = "admin@architonixlabs.com" });
        await adminRow.Locator("button:has-text('Deactivate')").ClickAsync();
        await _page.WaitForTimeoutAsync(600);
        await Assertions.Expect(_page.Locator(".dbt-toast").Last).ToContainTextAsync("last active Admin");
        await Assertions.Expect(adminRow).ToContainTextAsync("Active");
    }

    [Test]
    public async Task AdminSendResetLink_RecordsAuditEvent_VisibleOnTheAuditPage()
    {
        _page = await GlobalSetup.Server.Browser.NewPageAsync();
        await LoginAsync("admin@architonixlabs.com");

        await GotoAsync("/users");
        await _page.WaitForSelectorAsync("button[type=submit]");
        await WaitForCircuitAsync();

        var priyaRow = _page.Locator("tr", new PageLocatorOptions { HasText = "priya.nair@architonixlabs.com" });
        await priyaRow.Locator("button:has-text('Send reset link')").ClickAsync();
        await _page.WaitForTimeoutAsync(600);

        // Testing environment has no email provider — the toast must say the link could not be
        // emailed rather than falsely claiming it was sent.
        await Assertions.Expect(_page.Locator(".dbt-toast")).ToContainTextAsync("Could not email");

        // Cross-cutting U19 check: the attempt is still recorded in the audit trail, searchable.
        await GotoAsync("/audit?");
        await _page.FillAsync("input[placeholder*='Search actor']", "PasswordResetLinkSent");
        await _page.WaitForTimeoutAsync(500);
        await Assertions.Expect(_page.Locator(".dbt-table tbody tr").First).ToContainTextAsync("PasswordResetLinkSent");
    }

    [Test]
    public async Task ForgotPassword_GivesNeutralResponse_WhetherOrNotTheAccountExists()
    {
        _page = await GlobalSetup.Server.Browser.NewPageAsync();
        await GotoAsync("/Account/ForgotPassword");
        await _page.WaitForSelectorAsync("#fp-email");
        await WaitForCircuitAsync();
        await _page.FillAsync("#fp-email", "priya.nair@architonixlabs.com");
        await _page.ClickAsync("button[type=submit]");
        await _page.WaitForSelectorAsync("text=If an account exists", new PageWaitForSelectorOptions { Timeout = 10000 });
        var existingAccountText = await _page.Locator("[data-testid=forgot-password-panel]").InnerTextAsync();

        await GotoAsync("/Account/ForgotPassword");
        await _page.WaitForSelectorAsync("#fp-email");
        await WaitForCircuitAsync();
        await _page.FillAsync("#fp-email", "definitely-nobody-e2e@architonixlabs.com");
        await _page.ClickAsync("button[type=submit]");
        await _page.WaitForSelectorAsync("text=If an account exists", new PageWaitForSelectorOptions { Timeout = 10000 });
        var nonExistentAccountText = await _page.Locator("[data-testid=forgot-password-panel]").InnerTextAsync();

        Assert.That(nonExistentAccountText, Does.Contain("If an account exists"));
        Assert.That(existingAccountText, Does.Contain("If an account exists"));
    }

    // Guards the App.razor render-mode carve-out: only /Account/Login is static SSR. This page lives
    // under /account too but needs the interactive circuit for its submit handler and toast.
    [Test]
    public async Task TemporaryPassword_ForcesChangePassword_AndTheNewPasswordSignsIn()
    {
        _page = await GlobalSetup.Server.Browser.NewPageAsync();
        await LoginAsync("admin@architonixlabs.com");

        await GotoAsync("/users");
        await _page.WaitForSelectorAsync("button[type=submit]");
        await WaitForCircuitAsync();
        var email = $"e2e-chpw-{Guid.NewGuid():N}@architonixlabs.com";
        await FillByLabel("Display name", "E2E Change Password User");
        await FillByLabel("Email", email);
        await FillByLabel("Temporary password", "Str0ngTempPass!");
        await SelectByLabel("Role", "Viewer");
        await _page.ClickAsync("button[type=submit]:text('Create user')");
        await Assertions.Expect(_page.Locator("tr", new PageLocatorOptions { HasText = email })).ToBeVisibleAsync();

        await LoginAsync(email, "Str0ngTempPass!");
        await _page.WaitForURLAsync(url => url.Contains("/account/change-password"), new PageWaitForURLOptions { Timeout = 15000 });
        await _page.WaitForSelectorAsync("text=choose your own before continuing");

        // G05: leaving the page doesn't skip the change, whether by a full load or by an in-app link.
        await GotoAsync("/instances");
        await _page.WaitForURLAsync(url => url.Contains("/account/change-password"), new PageWaitForURLOptions { Timeout = 15000 });
        await WaitForCircuitAsync();
        await _page.ClickAsync("a.app-nav-link[href='reports']");
        await _page.WaitForTimeoutAsync(800);
        Assert.That(_page.Url, Does.Contain("/account/change-password"), "In-app navigation must also return to the change-password page.");
        await WaitForCircuitAsync();

        await FillByLabel("Current password", "Str0ngTempPass!");
        await FillByLabel("New password", "N3wOwnPassword!");
        await _page.ClickAsync("button[type=submit]:text('Change password')");
        await Assertions.Expect(_page.Locator(".dbt-toast")).ToContainTextAsync("Password changed");
        await _page.WaitForURLAsync(url => !url.Contains("/account/change-password"), new PageWaitForURLOptions { Timeout = 15000 });

        var fresh = await GlobalSetup.Server.Browser.NewPageAsync();
        try
        {
            await fresh.GotoAsync(TestServerFixture.BaseUrl + "/Account/Login", new PageGotoOptions { WaitUntil = WaitUntilState.Load });
            await fresh.FillAsync("input[autocomplete~=username]", email);
            await fresh.FillAsync("input[autocomplete=current-password]", "N3wOwnPassword!");
            await fresh.ClickAsync("button[type=submit]");
            await fresh.WaitForURLAsync(url => !url.Contains("/Account/Login") && !url.Contains("/account/change-password"),
                new PageWaitForURLOptions { Timeout = 15000 });
            await fresh.WaitForSelectorAsync(".notif-bell-btn", new PageWaitForSelectorOptions { Timeout = 15000 });
        }
        finally
        {
            await fresh.CloseAsync();
        }
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
