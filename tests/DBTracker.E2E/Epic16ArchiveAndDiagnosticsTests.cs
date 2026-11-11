using Microsoft.Playwright;

namespace DBTracker.E2E;

/// <summary>E2E coverage for Epic 16's U14 (archive preview/unarchive/health recompute), U15 (admin setup diagnostics) and U18 (connectivity check with a custom port).</summary>
[TestFixture]
public class Epic16ArchiveAndDiagnosticsTests
{
    private IPage _page = null!;

    [TearDown]
    public async Task TearDown() => await _page.CloseAsync();

    [Test]
    public async Task ArchivingAnInstance_ShowsPreviewAndRequiresReason_ThenUnarchiveRestoresItToTheList()
    {
        _page = await GlobalSetup.Server.Browser.NewPageAsync();
        await LoginAsync("priya.nair@architonixlabs.com");

        await GotoAsync("/instances?s=DIFY-PLUGIN-PG");
        await _page.WaitForSelectorAsync(".dbt-table tbody tr");
        await WaitForCircuitAsync();
        await _page.ClickAsync(".dbt-table tbody tr");
        await _page.WaitForURLAsync(url => url.Contains("/instances/"));
        await WaitForCircuitAsync();

        await _page.ClickAsync("button:has-text('Archive')");
        await _page.WaitForTimeoutAsync(400);

        // Preview of what archiving affects is shown before the reason/confirm step.
        await Assertions.Expect(_page.Locator("text=Confirm archive")).ToBeVisibleAsync();
        await Assertions.Expect(_page.Locator("text=scan")).ToBeVisibleAsync();

        // No archive button until the kind is chosen; "decommissioned" points to the retirement checklist instead.
        var confirmButton = _page.Locator("button:has-text('Archive this instance')");
        await Assertions.Expect(confirmButton).ToHaveCountAsync(0);
        await _page.ClickAsync("[data-testid=archive-kind-retire]");
        await Assertions.Expect(_page.Locator("a:has-text('Open the retirement checklist')")).ToBeVisibleAsync();
        await Assertions.Expect(confirmButton).ToHaveCountAsync(0);

        // A duplicate can be archived directly, but not without a reason.
        await _page.ClickAsync("[data-testid=archive-kind-mistake]");
        await Assertions.Expect(confirmButton).ToBeDisabledAsync();
        await _page.FillAsync("#archive-reason", "E2E test archive — superseded by migration");
        await Assertions.Expect(confirmButton).ToBeEnabledAsync();
        await confirmButton.ClickAsync();
        await _page.WaitForURLAsync(url => url.Contains("/instances") && !url.Contains("DIFY"));
        await WaitForCircuitAsync();

        // The archived instance no longer appears in the normal (non-archived) list.
        await GotoAsync("/instances?s=DIFY-PLUGIN-PG");
        await _page.WaitForTimeoutAsync(600);
        await Assertions.Expect(_page.Locator(".dbt-table tbody tr")).ToHaveCountAsync(0);

        // It's visible and flagged on the Archived browser, with actor/reason/time.
        await GotoAsync("/instances/archived");
        await _page.WaitForSelectorAsync(".dbt-table tbody tr");
        await WaitForCircuitAsync();
        var archivedRow = _page.Locator("tr", new PageLocatorOptions { HasText = "DIFY-PLUGIN-PG" });
        await Assertions.Expect(archivedRow).ToContainTextAsync("priya.nair@architonixlabs.com");
        await Assertions.Expect(archivedRow).ToContainTextAsync("E2E test archive");

        // Unarchiving puts it back, and the instance detail no longer shows the archived banner.
        await archivedRow.Locator("button:has-text('Unarchive')").ClickAsync();
        // G12/U14: unarchive needs a reason, and the confirm stays disabled until one is given.
        await Assertions.Expect(_page.Locator("[data-testid=unarchive-confirm]")).ToBeDisabledAsync();
        await _page.FillAsync("[data-testid=unarchive-reason]", "Archived by mistake in an E2E test");
        await _page.ClickAsync("[data-testid=unarchive-confirm]");
        await _page.WaitForTimeoutAsync(600);
        await Assertions.Expect(_page.Locator(".dbt-table tbody tr", new PageLocatorOptions { HasText = "DIFY-PLUGIN-PG" })).ToHaveCountAsync(0);

        await GotoAsync("/instances?s=DIFY-PLUGIN-PG");
        await _page.WaitForSelectorAsync(".dbt-table tbody tr");
        await WaitForCircuitAsync();
        await _page.ClickAsync(".dbt-table tbody tr");
        await _page.WaitForURLAsync(url => url.Contains("/instances/"));
        await WaitForCircuitAsync();

        await Assertions.Expect(_page.Locator("text=is archived")).Not.ToBeVisibleAsync();
        // Health is recomputed fresh (not cached) once unarchived — the badge renders a real value again.
        await Assertions.Expect(_page.Locator("h1 .badge-health")).ToBeVisibleAsync();
    }

    [Test]
    public async Task SetupDiagnosticsPage_ShowsWorkerAndEmailChecks_AndTestEmailReportsRealOutcome()
    {
        _page = await GlobalSetup.Server.Browser.NewPageAsync();
        await LoginAsync("admin@architonixlabs.com");

        await GotoAsync("/setup");
        await _page.WaitForSelectorAsync(".dbt-table table");
        await WaitForCircuitAsync();

        await Assertions.Expect(_page.Locator("text=Backup scan")).ToBeVisibleAsync();
        await Assertions.Expect(_page.Locator("text=Alert check")).ToBeVisibleAsync();

        await _page.FillAsync("input[placeholder*='architonixlabs.com']", "diagnostics-test@architonixlabs.com");
        await _page.ClickAsync("button:has-text('Send test email')");
        await _page.WaitForTimeoutAsync(500);

        // Testing environment has no email provider configured — the real "Disabled" outcome must
        // show, not a false success.
        await Assertions.Expect(_page.Locator(".dbt-toast")).ToContainTextAsync("disabled");

        // Non-admin is blocked from this page entirely.
        await LoginAsync("priya.nair@architonixlabs.com");
        await GotoAsync("/setup");
        await _page.WaitForURLAsync(url => url.Contains("/Account/AccessDenied"));
    }

    [Test]
    public async Task ConnectivityCheck_WithCustomHostAndPort_ShowsActualEndpointAndTimeout()
    {
        _page = await GlobalSetup.Server.Browser.NewPageAsync();
        await LoginAsync("priya.nair@architonixlabs.com");

        await GotoAsync("/instances?s=ARXMAIL-PROD-01");
        await _page.WaitForSelectorAsync(".dbt-table tbody tr");
        await WaitForCircuitAsync();
        await _page.ClickAsync(".dbt-table tbody tr");
        await _page.WaitForURLAsync(url => url.Contains("/instances/"));
        await WaitForCircuitAsync();

        await _page.ClickAsync("a:has-text('Edit')");
        await _page.WaitForURLAsync(url => url.Contains("/edit"));
        await WaitForCircuitAsync();

        await _page.FillAsync("input[placeholder*='Defaults to Server IP']", "127.0.0.1");
        await _page.FillAsync("input[placeholder*='engine']", "59999");
        await _page.ClickAsync("button[type=submit]:has-text('Save')");
        await _page.WaitForURLAsync(url => url.Contains("/instances/") && !url.Contains("/edit"));
        await WaitForCircuitAsync();

        await _page.ClickAsync("button:has-text('Test connection')");
        await _page.WaitForTimeoutAsync(5500);

        // The actual overridden host:port is shown, plus the check time and timeout — not just a
        // generic "could not connect".
        await Assertions.Expect(_page.Locator("text=127.0.0.1:59999").First).ToBeVisibleAsync();
        await Assertions.Expect(_page.Locator("text=timeout").First).ToBeVisibleAsync();
        await Assertions.Expect(_page.Locator("text=not proof of database login")).ToBeVisibleAsync();
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
