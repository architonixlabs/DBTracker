using Microsoft.Playwright;

namespace DBTracker.E2E;

/// <summary>
/// E2E coverage for Epic 18 (D01-D06): database identity, releases and support lifecycle. Priya
/// (DBA) has Roles.CanEditPolicy, so one login covers both the per-instance Lifecycle page and the
/// admin-only /lifecycle catalogue hub.
/// </summary>
[TestFixture]
public class Epic18LifecycleTests
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

    private async Task GoToInstanceLifecycleAsync(string instanceSearchText)
    {
        await GotoAsync($"/instances?s={instanceSearchText}");
        await _page.WaitForSelectorAsync(".dbt-table tbody tr");
        await WaitForCircuitAsync();
        await _page.Locator(".instance-row-link").First.ClickAsync();
        await _page.WaitForURLAsync(url => url.Contains("/instances/"));
        await WaitForCircuitAsync();
        await _page.Locator("a[href$='/lifecycle']").ClickAsync();
        await _page.WaitForURLAsync(url => url.Contains("/lifecycle"));
        await WaitForCircuitAsync();
    }

    [Test]
    public async Task EnteringAnUnresolvableVersionString_ShowsUnresolvedState_NotSilentlyCompliant()
    {
        await GoToInstanceLifecycleAsync("AVALOKH-SALES-SQL");

        var identityCard = _page.Locator(".dbt-card", new PageLocatorOptions { HasText = "Installed-version identity" });
        var rawInput = identityCard.Locator("#raw-version");
        await rawInput.FillAsync("totally-not-a-real-version-string");
        await identityCard.GetByRole(AriaRole.Button, new LocatorGetByRoleOptions { Name = "Resolve & save" }).ClickAsync();
        await _page.WaitForTimeoutAsync(500);

        await Assertions.Expect(_page.Locator(".dbt-toast").First).ToContainTextAsync("saved");
        await Assertions.Expect(identityCard).ToContainTextAsync("Unresolved");
        await Assertions.Expect(identityCard).ToContainTextAsync("never treated as compliant");
    }

    [Test]
    public async Task EnteringARecognizedVersionString_ThenAttesting_ShowsResolvedAndAttested()
    {
        await GoToInstanceLifecycleAsync("AVALOKH-SALES-SQL");

        var identityCard = _page.Locator(".dbt-card", new PageLocatorOptions { HasText = "Installed-version identity" });
        await identityCard.Locator("#raw-version").FillAsync("2022");
        await identityCard.GetByRole(AriaRole.Button, new LocatorGetByRoleOptions { Name = "Resolve & save" }).ClickAsync();
        await _page.WaitForTimeoutAsync(500);

        await Assertions.Expect(identityCard).ToContainTextAsync("Resolved");

        await identityCard.GetByRole(AriaRole.Button, new LocatorGetByRoleOptions { Name = "Attest current identity" }).ClickAsync();
        await _page.WaitForTimeoutAsync(500);

        await Assertions.Expect(_page.Locator(".dbt-toast").First).ToContainTextAsync("attested");
        await Assertions.Expect(identityCard).ToContainTextAsync("Attested by");
    }

    [Test]
    public async Task SeededInstanceApproachingSupportDeadline_ShowsDaysRemaining_AndRaisesNotice()
    {
        await GoToInstanceLifecycleAsync("ARXMAIL-PROD-01");

        var runwayCard = _page.Locator(".dbt-card", new PageLocatorOptions { HasText = "Support runway" });
        await Assertions.Expect(runwayCard).ToContainTextAsync("Mainstream");
        await Assertions.Expect(runwayCard.Locator("[data-testid=runway-days]")).ToContainTextAsync("remaining");

        await runwayCard.GetByRole(AriaRole.Button, new LocatorGetByRoleOptions { Name = "Evaluate support-runway notices now (admin/DBA action, mirrors the background sweep)" }).ClickAsync();
        await _page.WaitForTimeoutAsync(800);

        await Assertions.Expect(_page.Locator(".dbt-toast").First).ToContainTextAsync("notice");

        await GotoAsync("/notifications");
        await WaitForCircuitAsync();
        await Assertions.Expect(_page.Locator("body")).ToContainTextAsync("Mainstream support ends");
    }

    [Test]
    public async Task PatchBaselineComparison_ShowsFourDistinctFacts()
    {
        await GoToInstanceLifecycleAsync("CONTOSO-HR-MYSQL");

        var patchCard = _page.Locator(".dbt-card", new PageLocatorOptions { HasText = "Release availability" });
        await Assertions.Expect(patchCard).ToContainTextAsync("MySQL 8.0 build 36"); // installed
        await Assertions.Expect(patchCard).ToContainTextAsync("MySQL 8.0.43"); // latest applicable branch patch AND approved target
        await Assertions.Expect(patchCard).ToContainTextAsync("MySQL 8.4 LTS"); // newest relevant GA
        await Assertions.Expect(patchCard).Not.ToContainTextAsync("None approved"); // an approved baseline was seeded
    }

    [Test]
    public async Task AdvisoryEvaluation_ClassifiesPotential_ThenReviewerConfirms()
    {
        await GoToInstanceLifecycleAsync("CONTOSO-HR-MYSQL");

        var advisoryCard = _page.Locator(".dbt-card", new PageLocatorOptions { HasText = "Advisory applicability" });
        await advisoryCard.GetByRole(AriaRole.Button, new LocatorGetByRoleOptions { Name = "Evaluate advisories for this instance" }).ClickAsync();
        await _page.WaitForTimeoutAsync(500);

        await Assertions.Expect(advisoryCard).ToContainTextAsync("CVE-2025-0001");
        await Assertions.Expect(advisoryCard).ToContainTextAsync("Potential");
        await Assertions.Expect(advisoryCard).ToContainTextAsync("Version matching alone cannot confirm vulnerability");

        await advisoryCard.GetByRole(AriaRole.Button, new LocatorGetByRoleOptions { Name = "Confirm" }).First.ClickAsync();
        var form = advisoryCard.Locator("[data-testid=advisory-review-form]");
        await Assertions.Expect(form).ToBeVisibleAsync();

        // A confirmed exposure needs evidence, an owner and a deadline; saving without evidence is refused.
        await form.Locator("[data-testid=advisory-review-save]").ClickAsync();
        await Assertions.Expect(form.Locator("[role=alert]")).ToContainTextAsync("Evidence is required");

        await form.Locator("[data-testid=advisory-review-evidence]").FillAsync("Build 8.0.36 confirmed; the affected plugin is enabled.");
        await form.Locator("[data-testid=advisory-review-owner]").FillAsync("Priya Nair");
        await form.Locator("[data-testid=advisory-review-deadline]").FillAsync(DateTime.UtcNow.AddDays(14).ToString("yyyy-MM-dd"));
        await form.Locator("[data-testid=advisory-review-target]").FillAsync("8.0.41");
        await form.Locator("[data-testid=advisory-review-save]").ClickAsync();
        await _page.WaitForTimeoutAsync(500);

        await Assertions.Expect(_page.Locator(".dbt-toast").First).ToContainTextAsync("reviewed");
        await Assertions.Expect(advisoryCard).ToContainTextAsync("Reviewed by priya.nair@architonixlabs.com");
        var summary = advisoryCard.Locator("[data-testid=advisory-review-summary]").First;
        await Assertions.Expect(summary).ToContainTextAsync("Owner: Priya Nair");
        await Assertions.Expect(summary).ToContainTextAsync("Target patch: 8.0.41");
        await Assertions.Expect(summary).ToContainTextAsync("Unverified");
    }

    [Test]
    public async Task CatalogueAdminView_CreateEntry_ThenManualOverride()
    {
        await GotoAsync("/lifecycle");
        await WaitForCircuitAsync();

        await _page.FillAsync("input[placeholder='Family (e.g. PostgreSQL)']", "PostgreSQL");
        await _page.FillAsync("input[placeholder='Release label (e.g. PostgreSQL 16)']", "PostgreSQL 15 (E2E)");
        await _page.GetByRole(AriaRole.Button, new PageGetByRoleOptions { Name = "Add entry" }).ClickAsync();
        await _page.WaitForTimeoutAsync(500);

        await Assertions.Expect(_page.Locator(".dbt-toast").First).ToContainTextAsync("added");
        var entryCard = _page.Locator(".dbt-card", new PageLocatorOptions { HasText = "PostgreSQL 15 (E2E)" });
        await Assertions.Expect(entryCard).ToBeVisibleAsync();

        await entryCard.GetByRole(AriaRole.Button, new LocatorGetByRoleOptions { Name = "Manual override…" }).ClickAsync();
        await entryCard.Locator("input[placeholder='Reason (required)']").FillAsync("Vendor page republished with a corrected EOL date");
        await entryCard.Locator("input[placeholder='Reviewed by (required)']").FillAsync("Priya Nair");
        await entryCard.GetByRole(AriaRole.Button, new LocatorGetByRoleOptions { Name = "Apply override" }).ClickAsync();
        await _page.WaitForTimeoutAsync(500);

        await Assertions.Expect(_page.Locator(".dbt-toast").First).ToContainTextAsync("override applied");
        await Assertions.Expect(entryCard).ToContainTextAsync("manually overridden");
    }

    [Test]
    public async Task CatalogueRefreshWithNoSourceUrl_ReportsStaleness_RetainsLastKnownFacts()
    {
        await GotoAsync("/lifecycle");
        await WaitForCircuitAsync();

        await _page.FillAsync("input[placeholder='Family (e.g. PostgreSQL)']", "Oracle Database");
        await _page.FillAsync("input[placeholder='Release label (e.g. PostgreSQL 16)']", "Oracle 19c (E2E no-source)");
        await _page.GetByRole(AriaRole.Button, new PageGetByRoleOptions { Name = "Add entry" }).ClickAsync();
        await _page.WaitForTimeoutAsync(500);

        var entryCard = _page.Locator(".dbt-card", new PageLocatorOptions { HasText = "Oracle 19c (E2E no-source)" });
        await entryCard.GetByRole(AriaRole.Button, new LocatorGetByRoleOptions { Name = "Refresh" }).ClickAsync();
        await _page.WaitForTimeoutAsync(500);

        await Assertions.Expect(_page.Locator(".dbt-toast").First).ToContainTextAsync("Refresh failed");
        await Assertions.Expect(entryCard).ToContainTextAsync("stale");
        // The release label itself (a "last-known fact") must still be shown unchanged.
        await Assertions.Expect(entryCard).ToContainTextAsync("Oracle 19c (E2E no-source)");
    }

    private async Task LoginAsync(string email)
    {
        await GotoAsync("/Account/Login");
        await _page.FillAsync("input[autocomplete~=username]", email);
        await _page.FillAsync("input[autocomplete=current-password]", "ArxDemo#2026!");

        await _page.ClickAsync("button[type=submit]");
        await _page.WaitForURLAsync(url => !url.Contains("/Account/Login"), new PageWaitForURLOptions { Timeout = 15000 });

        await WaitForCircuitAsync();
    }

    private Task GotoAsync(string path) =>
        _page.GotoAsync(TestServerFixture.BaseUrl + path, new PageGotoOptions { WaitUntil = WaitUntilState.Load });

    private Task WaitForCircuitAsync() => _page.WaitForTimeoutAsync(800);
}
