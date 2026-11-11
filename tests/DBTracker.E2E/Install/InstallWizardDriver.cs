using Microsoft.Playwright;

namespace DBTracker.InstallE2E;

/// <summary>Drives the wizard the way an installer would, with Windows authentication against the local SQL Server.</summary>
public static class InstallWizardDriver
{
    public const string AdminEmail = "first.admin@example.com";
    public const string AdminPassword = "Install2026!";

    public static Task<IResponse?> GotoAsync(IPage page, string path) =>
        page.GotoAsync(InstallServerFixture.BaseUrl + path, new() { WaitUntil = WaitUntilState.Load });

    public static async Task OpenAsync(IPage page)
    {
        await GotoAsync(page, "/install");
        await page.WaitForSelectorAsync("#install-key");
        // The prerendered form is visible before the circuit attaches; give it time so clicks reach the server.
        await page.WaitForTimeoutAsync(800);
    }

    public static Task ExpectStepAsync(IPage page, string title) =>
        Assertions.Expect(page.Locator("h2.install-step-title")).ToHaveTextAsync(title, new() { Timeout = 30000 });

    public static async Task CompleteAsync(IPage page, InstallServerFixture server, bool loadSampleData)
    {
        await OpenAsync(page);
        await page.FillAsync("#install-key", server.ReadSetupKey());
        await page.ClickAsync("button:has-text('Verify setup key')");
        await ExpectStepAsync(page, "Connect to SQL Server");

        await page.FillAsync("#install-server", "localhost");
        await page.FillAsync("#install-database", server.DatabaseName);
        await page.CheckAsync("#install-trust-cert");
        await page.ClickAsync("button:has-text('Test connection')");
        await Assertions.Expect(page.Locator(".alert[role=status]")).ToContainTextAsync("doesn't exist yet", new() { Timeout = 30000 });

        await page.ClickAsync($"button:has-text('Create database {server.DatabaseName}')");
        await Assertions.Expect(page.Locator(".alert[role=status]")).ToContainTextAsync("is empty", new() { Timeout = 30000 });
        await page.ClickAsync("button:has-text('Use this database')");
        await ExpectStepAsync(page, "Create the tables");

        var rows = page.Locator(".install-migrations li");
        Assert.That(await rows.CountAsync(), Is.GreaterThan(5));
        await page.ClickAsync("button:has-text('Create tables')");
        await page.Locator("button:has-text('Continue')").WaitForAsync(new() { Timeout = 180000 });
        Assert.That(await page.Locator(".install-migrations li.done").CountAsync(), Is.EqualTo(await rows.CountAsync()));
        await page.ClickAsync("button:has-text('Continue')");

        await ExpectStepAsync(page, "Sample data");
        Assert.That(await page.IsCheckedAsync("#install-sample"), Is.False, "sample data must be off by default");
        if (loadSampleData)
        {
            await page.CheckAsync("#install-sample");
            await Assertions.Expect(page.Locator(".alert-warning")).ToContainTextAsync("Not for production");
        }
        await page.ClickAsync("button:has-text('Continue')");

        await ExpectStepAsync(page, "Create the Administrator account");
        await page.FillAsync("#install-admin-name", "First Admin");
        await page.FillAsync("#install-admin-email", AdminEmail);
        await page.FillAsync("#install-admin-password", AdminPassword);
        await page.FillAsync("#install-admin-confirm", AdminPassword);
        await page.ClickAsync("button[type=submit]:has-text('Continue')");

        await ExpectStepAsync(page, "Email delivery");
        await page.CheckAsync("#install-email-none");
        await page.ClickAsync("button[type=submit]:has-text('Continue')");

        await ExpectStepAsync(page, "Check a backup share");
        await page.ClickAsync("button:has-text('Skip this check')");

        await ExpectStepAsync(page, "Review and finish");
        await Assertions.Expect(page.Locator(".install-summary")).ToContainTextAsync(server.DatabaseName);
        await Assertions.Expect(page.Locator(".install-summary")).ToContainTextAsync(AdminEmail);
        await page.ClickAsync("button:has-text('Finish setup')");
        await page.WaitForURLAsync(url => url.Contains("/Account/Login"), new() { Timeout = 60000 });
    }

    public static async Task LogInAsync(IPage page, string email, string password)
    {
        await GotoAsync(page, "/Account/Login");
        await page.FillAsync("input[autocomplete=username]", email);
        await page.FillAsync("input[autocomplete=current-password]", password);
        await page.ClickAsync("button[type=submit]");
        await page.WaitForURLAsync(url => !url.Contains("/Account/Login"), new() { Timeout = 15000 });
    }
}
