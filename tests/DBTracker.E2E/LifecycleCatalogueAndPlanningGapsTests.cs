using System.Text;
using System.Text.RegularExpressions;
using Microsoft.Playwright;

namespace DBTracker.E2E;

/// <summary>
/// E2E coverage for G13/G14: lifecycle CSV imports that are safe to repeat, the fuller catalogue form, a failed version
/// read being recorded, and a campaign that can't reach Ready without rehearsal evidence or Scheduled without approval.
/// </summary>
[TestFixture]
public class LifecycleCatalogueAndPlanningGapsTests
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

    [Test]
    public async Task CatalogueCsvImport_PreviewsThenApplies_AndAReimportIsSkipped()
    {
        var label = $"PostgreSQL 13 import {Guid.NewGuid():N}"[..36];
        var csv = "Engine,Family,ReleaseLabel,Major,Channel,MainstreamSupportEnd,SourceUrl,ReleaseNotesUrl\n" +
                  "# example comment row is ignored\n" +
                  $"PostgreSql,PostgreSQL,{label},13,GA,2025-11-13,https://www.postgresql.org/support/versioning/,https://www.postgresql.org/docs/release/13.0/\n" +
                  "PostgreSql,PostgreSQL,Bad row,13,GA,not-a-date,,\n";

        await GotoAsync("/lifecycle/import");
        await WaitForCircuitAsync();
        await _page.SetInputFilesAsync("[data-testid=lifecycle-import-file]", new FilePayload { Name = "catalogue.csv", MimeType = "text/csv", Buffer = Encoding.UTF8.GetBytes(csv) });

        var summary = _page.Locator("[data-testid=lifecycle-import-summary]");
        await Assertions.Expect(summary).ToContainTextAsync("1 create");
        await Assertions.Expect(summary).ToContainTextAsync("1 row(s) with errors");
        await Assertions.Expect(_page.Locator("[data-testid=lifecycle-import-errors]")).ToContainTextAsync("MainstreamSupportEnd");

        await _page.Locator("[data-testid=lifecycle-import-apply]").ClickAsync();
        await Assertions.Expect(_page.Locator(".dbt-toast").First).ToContainTextAsync("1 created");

        // The same file again: the release is now unchanged, so nothing would be written.
        await _page.SetInputFilesAsync("[data-testid=lifecycle-import-file]", new FilePayload { Name = "catalogue.csv", MimeType = "text/csv", Buffer = Encoding.UTF8.GetBytes(csv) });
        await Assertions.Expect(summary).ToContainTextAsync("0 create");
        await Assertions.Expect(summary).ToContainTextAsync("1 unchanged");

        await GotoAsync("/lifecycle");
        await WaitForCircuitAsync();
        var entryCard = _page.Locator(".dbt-card", new PageLocatorOptions { HasText = label });
        await Assertions.Expect(entryCard.Locator("[data-testid=catalogue-vendor-facts]")).ToContainTextAsync("2025-11-13");
        await Assertions.Expect(entryCard).ToContainTextAsync("Release notes");
        await Assertions.Expect(entryCard.Locator("[data-testid=catalogue-revisions]")).ToContainTextAsync("Created by CSV import");
    }

    [Test]
    public async Task AdvisoryCsvTemplate_DownloadsWithExampleRow()
    {
        await GotoAsync("/lifecycle/import");
        await WaitForCircuitAsync();
        await _page.Locator("[data-testid=import-kind-Advisories]").ClickAsync();

        var downloadTask = _page.WaitForDownloadAsync();
        await _page.Locator("[data-testid=import-template-button]").ClickAsync();
        var download = await downloadTask;
        Assert.That(download.SuggestedFilename, Is.EqualTo("dbtracker-advisories-template.csv"));
    }

    [Test]
    public async Task RecordingAFailedVersionRead_KeepsTheIdentity_AndShowsTheFailure()
    {
        await GotoAsync("/instances?s=CONTOSO-HR-MYSQL");
        await _page.WaitForSelectorAsync(".dbt-table tbody tr");
        await WaitForCircuitAsync();
        await _page.Locator(".instance-row-link").First.ClickAsync();
        await _page.WaitForURLAsync(url => url.Contains("/instances/"));
        await WaitForCircuitAsync();
        await _page.Locator("a[href$='/lifecycle']").ClickAsync();
        await _page.WaitForURLAsync(url => url.Contains("/lifecycle"));
        await WaitForCircuitAsync();

        var card = _page.Locator(".dbt-card", new PageLocatorOptions { HasText = "Version attestation" });
        await card.Locator("[data-testid=observation-outcome]").SelectOptionAsync("PermissionDenied");
        await card.Locator("[data-testid=observation-failure]").FillAsync("VIEW SERVER STATE denied for the inventory login (E2E)");
        await card.GetByRole(AriaRole.Button, new LocatorGetByRoleOptions { Name = "Record observation" }).ClickAsync();
        await _page.WaitForTimeoutAsync(500);

        await Assertions.Expect(_page.Locator(".dbt-toast").First).ToContainTextAsync("declared identity is unchanged");
        await Assertions.Expect(card.Locator("[data-testid=observation-row]").First).ToContainTextAsync("PermissionDenied");
        var identityCard = _page.Locator(".dbt-card", new PageLocatorOptions { HasText = "Installed-version identity" });
        await Assertions.Expect(identityCard).ToContainTextAsync("Resolved");
    }

    [Test]
    public async Task Campaign_CannotReachReadyWithoutRehearsal_NorScheduledWithoutApproval()
    {
        var campaignId = await CreateCampaignAsync($"G14 gate campaign {Guid.NewGuid():N}"[..40], "CONTOSO-HR-MYSQL");
        await GotoAsync($"/campaigns/{campaignId}");
        await WaitForCircuitAsync();
        await TransitionAsync("Assessing");

        // No rehearsal yet: Ready is refused and the reason is shown before trying.
        var stateCard = _page.Locator(".dbt-card", new PageLocatorOptions { HasText = "State transition" });
        await stateCard.Locator("[data-testid=next-state-select]").SelectOptionAsync("Ready");
        await Assertions.Expect(stateCard.Locator("[data-testid=transition-gate]")).ToContainTextAsync("rehearsal Missing");
        await stateCard.Locator("[data-testid=transition-button]").ClickAsync();
        await _page.WaitForTimeoutAsync(500);
        await Assertions.Expect(_page.Locator(".dbt-toast").First).ToContainTextAsync("rejected");
        await Assertions.Expect(_page.Locator("[data-testid=campaign-detail-state]")).ToContainTextAsync("Assessing");

        // A rehearsal whose performance check wasn't performed still doesn't count.
        await RecordRehearsalAsync(campaignId, performance: "NotPerformed");
        await GotoAsync($"/campaigns/{campaignId}");
        await WaitForCircuitAsync();
        stateCard = _page.Locator(".dbt-card", new PageLocatorOptions { HasText = "State transition" });
        await stateCard.Locator("[data-testid=next-state-select]").SelectOptionAsync("Ready");
        await Assertions.Expect(stateCard.Locator("[data-testid=transition-gate]")).ToContainTextAsync("performance comparison was not performed");

        await RecordRehearsalAsync(campaignId, performance: "Passed");
        await GotoAsync($"/campaigns/{campaignId}");
        await WaitForCircuitAsync();
        await TransitionAsync("Ready");

        // Scheduled needs an approval.
        stateCard = _page.Locator(".dbt-card", new PageLocatorOptions { HasText = "State transition" });
        await stateCard.Locator("[data-testid=next-state-select]").SelectOptionAsync("Scheduled");
        await Assertions.Expect(stateCard.Locator("[data-testid=transition-gate]")).ToContainTextAsync("No approval is recorded");

        var approvalCard = _page.Locator(".dbt-card", new PageLocatorOptions { HasText = "Approval history" });
        await approvalCard.Locator("input[placeholder='Scope (e.g. DBA technical)']").FillAsync("Application owner");
        await approvalCard.Locator("input[placeholder='Approver name']").FillAsync("Dev Patel");
        await approvalCard.Locator("[data-testid=approval-notes-input]").FillAsync("Approved after the regression suite passed");
        await approvalCard.GetByRole(AriaRole.Button, new LocatorGetByRoleOptions { Name = "Record approval" }).ClickAsync();
        await _page.WaitForTimeoutAsync(500);
        await Assertions.Expect(approvalCard.Locator("[data-testid=approval-notes]").First).ToContainTextAsync("regression suite");

        await TransitionAsync("Scheduled");

        // Planning fields are editable after creation.
        await _page.Locator("[data-testid=plan-edit]").ClickAsync();
        await _page.Locator("[data-testid=plan-milestones]").FillAsync("Rehearsal done\nCutover 2026-12-05");
        await _page.Locator("[data-testid=plan-effort]").FillAsync("16");
        await _page.Locator("[data-testid=plan-save]").ClickAsync();
        await _page.WaitForTimeoutAsync(500);
        await Assertions.Expect(_page.Locator("[data-testid=campaign-milestones]")).ToContainTextAsync("Cutover 2026-12-05");
        await Assertions.Expect(_page.Locator("[data-testid=campaign-effort]")).ToContainTextAsync("16 h");
    }

    private async Task RecordRehearsalAsync(int campaignId, string performance)
    {
        await GotoAsync($"/campaigns/{campaignId}/rehearsals");
        await WaitForCircuitAsync();
        await _page.Locator("[data-testid=rehearsal-instance-select]").SelectOptionAsync(new SelectOptionValue { Label = "CONTOSO-HR-MYSQL" });
        await _page.FillAsync("input[placeholder='Source label']", "MySQL 8.0");
        await _page.FillAsync("input[placeholder='Target label']", "MySQL 8.4");
        await _page.Locator("[data-testid=backup-restore-outcome]").SelectOptionAsync("Passed");
        await _page.Locator("[data-testid=correctness-outcome]").SelectOptionAsync("Passed");
        await _page.Locator("[data-testid=performance-outcome]").SelectOptionAsync(performance);
        await _page.Locator("[data-testid=write-disposition]").SelectOptionAsync("NotApplicable");
        await _page.Locator("[data-testid=record-rehearsal-button]").ClickAsync();
        await _page.WaitForTimeoutAsync(500);
    }

    private async Task TransitionAsync(string toState)
    {
        var stateCard = _page.Locator(".dbt-card", new PageLocatorOptions { HasText = "State transition" });
        await stateCard.Locator("[data-testid=next-state-select]").SelectOptionAsync(toState);
        await stateCard.Locator("input[placeholder='Reason']").FillAsync($"Move to {toState}");
        await stateCard.Locator("[data-testid=transition-button]").ClickAsync();
        await _page.WaitForTimeoutAsync(500);
        await Assertions.Expect(_page.Locator("[data-testid=campaign-detail-state]")).ToContainTextAsync(toState);
    }

    private async Task<int> CreateCampaignAsync(string name, string instanceLabelText)
    {
        await GotoAsync("/campaigns");
        await WaitForCircuitAsync();
        await _page.FillAsync("input[placeholder='Campaign name']", name);
        await _page.FillAsync("input[placeholder='Source label']", "Source");
        await _page.FillAsync("input[placeholder='Target label']", "Target");
        await _page.Locator("label", new PageLocatorOptions { HasText = instanceLabelText }).ClickAsync();
        await _page.GetByRole(AriaRole.Button, new PageGetByRoleOptions { Name = "Create campaign" }).ClickAsync();
        await _page.WaitForURLAsync(url => url.Contains("/campaigns/"), new PageWaitForURLOptions { Timeout = 15000 });
        await WaitForCircuitAsync();
        return int.Parse(Regex.Match(_page.Url, @"/campaigns/(\d+)").Groups[1].Value);
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
