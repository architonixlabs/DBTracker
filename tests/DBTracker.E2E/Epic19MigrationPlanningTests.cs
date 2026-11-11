using Microsoft.Playwright;

namespace DBTracker.E2E;

/// <summary>
/// E2E coverage for Epic 19 (D07-D10): dependency assessment and migration planning. Priya (DBA)
/// has Roles.CanEditPolicy, which covers the per-instance Migration page (D07-D09), the admin
/// /lifecycle "Path rules" tab (D08's curated rule set) and the /campaigns hub (D10).
/// </summary>
[TestFixture]
public class Epic19MigrationPlanningTests
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

    private async Task GoToInstanceMigrationAsync(string instanceSearchText)
    {
        await GotoAsync($"/instances?s={instanceSearchText}");
        await _page.WaitForSelectorAsync(".dbt-table tbody tr");
        await WaitForCircuitAsync();
        await _page.Locator(".instance-row-link").First.ClickAsync();
        await _page.WaitForURLAsync(url => url.Contains("/instances/"));
        await WaitForCircuitAsync();
        await _page.Locator("a[href$='/migration']").ClickAsync();
        await _page.WaitForURLAsync(url => url.Contains("/migration"));
        await WaitForCircuitAsync();
    }

    [Test]
    public async Task RecordingAnUnresolvedDependency_FlagsItBeforeAChangeToTheSharedEngine()
    {
        await GoToInstanceMigrationAsync("AVALOKH-SALES-SQL");

        // No dependencies recorded yet — the "before you change a shared engine" banner is absent.
        await Assertions.Expect(_page.Locator("[data-testid=dependency-impact-banner]")).Not.ToBeVisibleAsync();

        var depCard = _page.Locator(".dbt-card", new PageLocatorOptions { HasText = "Technical dependencies" });
        await depCard.Locator("input[placeholder='Name']").FillAsync("Legacy reporting ODBC driver");
        await depCard.GetByRole(AriaRole.Button, new LocatorGetByRoleOptions { Name = "Record dependency" }).ClickAsync();
        await _page.WaitForTimeoutAsync(500);

        await Assertions.Expect(_page.Locator(".dbt-toast").First).ToContainTextAsync("recorded");
        await Assertions.Expect(depCard).ToContainTextAsync("Legacy reporting ODBC driver");
        await Assertions.Expect(depCard).ToContainTextAsync("Unresolved");

        var banner = _page.Locator("[data-testid=dependency-impact-banner]");
        await Assertions.Expect(banner).ToBeVisibleAsync();
        await Assertions.Expect(banner).ToContainTextAsync("Before changing this shared engine");
        await Assertions.Expect(banner).ToContainTextAsync("1 unresolved dependency");

        // Marking it resolved clears the concern.
        await depCard.GetByRole(AriaRole.Button, new LocatorGetByRoleOptions { Name = "Mark resolved" }).ClickAsync();
        await _page.WaitForTimeoutAsync(500);
        await Assertions.Expect(depCard).ToContainTextAsync("Resolved");
    }

    [Test]
    public async Task PathAssessment_NoRuleIsUnknown_ThenAMatchingReviewedRuleMakesItSupported()
    {
        const string sourceFamily = "SQL Server 2019 (E2E)";
        const string targetFamily = "SQL Server 2022 (E2E)";

        await GoToInstanceMigrationAsync("AVALOKH-SALES-SQL");

        var pathCard = _page.Locator(".dbt-card", new PageLocatorOptions { HasText = "Source-to-target path assessment" });
        await pathCard.Locator("input[placeholder='Source product/family']").FillAsync(sourceFamily);
        await pathCard.Locator("input[placeholder='Source build']").FillAsync("15.0.2000");
        await pathCard.Locator("input[placeholder='Target product/family']").FillAsync(targetFamily);
        await pathCard.Locator("input[placeholder='Target build']").FillAsync("16.0.1000");
        await pathCard.GetByRole(AriaRole.Button, new LocatorGetByRoleOptions { Name = "Evaluate path" }).ClickAsync();
        await _page.WaitForTimeoutAsync(500);

        // No reviewed rule exists for this pairing yet — the absence of a rule must be Unknown, never Supported.
        var firstStatus = pathCard.Locator("[data-testid=path-status]").First;
        await Assertions.Expect(firstStatus).ToContainTextAsync("Unknown");
        await Assertions.Expect(pathCard.Locator("[data-testid=path-reasoning]").First).ToContainTextAsync("never Supported");

        // Curate a reviewed rule covering exactly this pairing.
        await GotoAsync("/lifecycle");
        await WaitForCircuitAsync();
        await _page.GetByRole(AriaRole.Button, new PageGetByRoleOptions { Name = "Path rules" }).ClickAsync();

        var ruleCard = _page.Locator(".dbt-card", new PageLocatorOptions { HasText = "Add a reviewed path rule" });
        await ruleCard.Locator("select").First.SelectOptionAsync("SqlServer");
        await ruleCard.Locator("input[placeholder='Source family']").FillAsync(sourceFamily);
        await ruleCard.Locator("input[placeholder='Target family']").FillAsync(targetFamily);
        await ruleCard.Locator("input[placeholder='Src build min']").FillAsync("15.0.0");
        await ruleCard.Locator("input[placeholder='Src build max']").FillAsync("15.0.9999");
        await ruleCard.Locator("input[placeholder='Tgt build min']").FillAsync("16.0.0");
        await ruleCard.Locator("input[placeholder='Tgt build max']").FillAsync("16.0.9999");
        await ruleCard.Locator("input[placeholder='Reviewed by (required)']").FillAsync("Angela Price");
        await ruleCard.GetByRole(AriaRole.Button, new LocatorGetByRoleOptions { Name = "Add rule" }).ClickAsync();
        await _page.WaitForTimeoutAsync(500);
        await Assertions.Expect(_page.Locator(".dbt-toast").First).ToContainTextAsync("added");

        // Re-evaluating the exact same pairing now matches the reviewed rule.
        await GoToInstanceMigrationAsync("AVALOKH-SALES-SQL");
        var pathCard2 = _page.Locator(".dbt-card", new PageLocatorOptions { HasText = "Source-to-target path assessment" });
        await pathCard2.Locator("input[placeholder='Source product/family']").FillAsync(sourceFamily);
        await pathCard2.Locator("input[placeholder='Source build']").FillAsync("15.0.2000");
        await pathCard2.Locator("input[placeholder='Target product/family']").FillAsync(targetFamily);
        await pathCard2.Locator("input[placeholder='Target build']").FillAsync("16.0.1000");
        await pathCard2.GetByRole(AriaRole.Button, new LocatorGetByRoleOptions { Name = "Evaluate path" }).ClickAsync();
        await _page.WaitForTimeoutAsync(500);

        await Assertions.Expect(pathCard2.Locator("[data-testid=path-status]").First).ToContainTextAsync("Supported");
    }

    [Test]
    public async Task ReadinessChecklist_StartsAllUnknown_ThenOneFailedCheckIsBlocking()
    {
        await GoToInstanceMigrationAsync("CONTOSO-HR-MYSQL");

        var readyCard = _page.Locator(".dbt-card", new PageLocatorOptions { HasText = "Upgrade-readiness checklist" });
        await readyCard.Locator("input[placeholder='Source label']").FillAsync("MySQL 8.0.36 (E2E)");
        await readyCard.Locator("input[placeholder='Target label']").FillAsync("MySQL 8.4 LTS (E2E)");
        await readyCard.GetByRole(AriaRole.Button, new LocatorGetByRoleOptions { Name = "New checklist" }).ClickAsync();
        await _page.WaitForTimeoutAsync(500);

        await Assertions.Expect(_page.Locator(".dbt-toast").First).ToContainTextAsync("Unknown");
        await Assertions.Expect(readyCard.Locator("[data-testid=readiness-summary]")).ToContainTextAsync("9 unknown");

        var driverItem = readyCard.Locator("[data-testid=readiness-item]", new LocatorLocatorOptions { HasText = "Driver/ORM compatibility" });
        await Assertions.Expect(driverItem.Locator("[data-testid=readiness-status]")).ToContainTextAsync("Unknown");

        await driverItem.GetByRole(AriaRole.Button, new LocatorGetByRoleOptions { Name = "Record result" }).ClickAsync();
        await driverItem.Locator("select").SelectOptionAsync("Failed");
        await driverItem.Locator("input[placeholder='Reviewer']").FillAsync("Priya Nair");
        await driverItem.Locator("input[placeholder='Findings']").FillAsync("ODBC driver 3.x is not certified against the target edition.");
        await driverItem.GetByRole(AriaRole.Button, new LocatorGetByRoleOptions { Name = "Save check" }).ClickAsync();
        await _page.WaitForTimeoutAsync(500);

        await Assertions.Expect(_page.Locator(".dbt-toast").First).ToContainTextAsync("saved");
        await Assertions.Expect(driverItem.Locator("[data-testid=readiness-status]")).ToContainTextAsync("Failed");
        await Assertions.Expect(readyCard.Locator("[data-testid=readiness-summary]")).ToContainTextAsync("1 failed");
        await Assertions.Expect(readyCard.Locator("[data-testid=readiness-summary]")).ToContainTextAsync("8 unknown");
        await Assertions.Expect(readyCard.Locator("[data-testid=readiness-summary]")).ToContainTextAsync("blocking finding");

        // The other checks were never touched and must stay honestly Unknown, not Passed.
        var extensionsItem = readyCard.Locator("[data-testid=readiness-item]", new LocatorLocatorOptions { HasText = "Extensions/plugins" });
        await Assertions.Expect(extensionsItem.Locator("[data-testid=readiness-status]")).ToContainTextAsync("Unknown");
    }

    [Test]
    public async Task MigrationCampaign_ComparesAlternatives_AndTransitionsState()
    {
        var campaignName = $"Avalokh SQL 2022 upgrade (E2E {Guid.NewGuid():N})"[..60];

        await GotoAsync("/campaigns");
        await WaitForCircuitAsync();

        await _page.FillAsync("input[placeholder='Campaign name']", campaignName);
        await _page.FillAsync("input[placeholder='Source label']", "SQL Server 2019");
        await _page.FillAsync("input[placeholder='Target label']", "SQL Server 2022");
        await _page.FillAsync("input[placeholder='Reason']", "Mainstream support end approaching");
        await _page.Locator("label", new PageLocatorOptions { HasText = "AVALOKH-SALES-SQL" }).ClickAsync();
        await _page.GetByRole(AriaRole.Button, new PageGetByRoleOptions { Name = "Create campaign" }).ClickAsync();
        await _page.WaitForURLAsync(url => url.Contains("/campaigns/"), new PageWaitForURLOptions { Timeout = 15000 });
        await WaitForCircuitAsync();

        await Assertions.Expect(_page.Locator("[data-testid=campaign-detail-state]")).ToContainTextAsync("Proposed");

        // Record and compare two alternatives rather than assuming upgrade is the only option.
        var altCard = _page.Locator(".dbt-card", new PageLocatorOptions { HasText = "Alternatives compared" });
        await altCard.Locator("input[placeholder='Description']").FillAsync("Upgrade in place to SQL Server 2022");
        await altCard.GetByRole(AriaRole.Button, new LocatorGetByRoleOptions { Name = "Add alternative" }).ClickAsync();
        await _page.WaitForTimeoutAsync(500);

        await altCard.Locator("select").SelectOptionAsync("Retire");
        await altCard.Locator("input[placeholder='Description']").FillAsync("Retire and move consumers to a replacement service");
        await altCard.GetByRole(AriaRole.Button, new LocatorGetByRoleOptions { Name = "Add alternative" }).ClickAsync();
        await _page.WaitForTimeoutAsync(500);

        var retireRow = altCard.Locator("[data-testid=alternative-row]", new LocatorLocatorOptions { HasText = "Retire" });
        await retireRow.GetByRole(AriaRole.Button, new LocatorGetByRoleOptions { Name = "Choose" }).ClickAsync();
        await _page.WaitForTimeoutAsync(500);
        await Assertions.Expect(retireRow).ToContainTextAsync("Chosen");

        // Valid state transition, recorded in history.
        var stateCard = _page.Locator(".dbt-card", new PageLocatorOptions { HasText = "State transition" });
        await stateCard.Locator("[data-testid=next-state-select]").SelectOptionAsync("Assessing");
        await stateCard.Locator("input[placeholder='Reason']").FillAsync("Starting technical assessment");
        await stateCard.Locator("[data-testid=transition-button]").ClickAsync();
        await _page.WaitForTimeoutAsync(500);

        await Assertions.Expect(_page.Locator(".dbt-toast").First).ToContainTextAsync("Assessing");
        await Assertions.Expect(_page.Locator("[data-testid=campaign-detail-state]")).ToContainTextAsync("Assessing");
        await Assertions.Expect(stateCard.Locator("[data-testid=transition-history]")).ToContainTextAsync("Proposed");
        await Assertions.Expect(stateCard.Locator("[data-testid=transition-history]")).ToContainTextAsync("Assessing");

        // Approval history.
        var approvalCard = _page.Locator(".dbt-card", new PageLocatorOptions { HasText = "Approval history" });
        await approvalCard.Locator("input[placeholder='Scope (e.g. DBA technical)']").FillAsync("DBA technical");
        await approvalCard.Locator("input[placeholder='Approver name']").FillAsync("Priya Nair");
        await approvalCard.GetByRole(AriaRole.Button, new LocatorGetByRoleOptions { Name = "Record approval" }).ClickAsync();
        await _page.WaitForTimeoutAsync(500);

        await Assertions.Expect(approvalCard).ToContainTextAsync("Priya Nair");
        await Assertions.Expect(approvalCard).ToContainTextAsync("Approved");
    }

    private async Task LoginAsync(string email)
    {
        await GotoAsync("/Account/Login");
        await _page.FillAsync("input[autocomplete=username]", email);
        await _page.FillAsync("input[autocomplete=current-password]", "ArxDemo#2026!");

        await _page.ClickAsync("button[type=submit]");
        await _page.WaitForURLAsync(url => !url.Contains("/Account/Login"), new PageWaitForURLOptions { Timeout = 15000 });

        await WaitForCircuitAsync();
    }

    private Task GotoAsync(string path) =>
        _page.GotoAsync(TestServerFixture.BaseUrl + path, new PageGotoOptions { WaitUntil = WaitUntilState.Load });

    private Task WaitForCircuitAsync() => _page.WaitForTimeoutAsync(800);
}
