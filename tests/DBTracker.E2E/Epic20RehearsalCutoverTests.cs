using System.Text.RegularExpressions;
using Microsoft.Playwright;

namespace DBTracker.E2E;

/// <summary>
/// E2E coverage for Epic 20 (D11-D15): rehearsal/recovery evidence, scheduled change windows and
/// cutover journal, post-change validation and completion gating, the generic D14 exception
/// mechanism, and the D15 lifecycle workspace.
/// </summary>
[TestFixture]
public class Epic20RehearsalCutoverTests
{
    private IPage _page = null!;

    [SetUp]
    public async Task SetUp()
    {
        _page = await GlobalSetup.Server.Browser.NewPageAsync();
    }

    [TearDown]
    public async Task TearDown() => await _page.CloseAsync();

    [Test]
    public async Task RecordingAFailedRehearsal_BlocksReadiness()
    {
        await LoginAsync("priya.nair@architonixlabs.com");
        var campaignId = await CreateCampaignAsync($"D11 rehearsal campaign {Guid.NewGuid():N}"[..50], "AVALOKH-SALES-SQL");

        await GotoAsync($"/campaigns/{campaignId}/rehearsals");
        await WaitForCircuitAsync();

        await _page.Locator("[data-testid=rehearsal-instance-select]").SelectOptionAsync(new SelectOptionValue { Label = "AVALOKH-SALES-SQL" });
        await _page.FillAsync("input[placeholder='Source label']", "SQL Server 2019");
        await _page.FillAsync("input[placeholder='Target label']", "SQL Server 2022");
        await _page.Locator("[data-testid=backup-restore-outcome]").SelectOptionAsync("Failed");
        await _page.Locator("[data-testid=correctness-outcome]").SelectOptionAsync("Passed");

        await _page.Locator("[data-testid=record-rehearsal-button]").ClickAsync();
        await _page.WaitForTimeoutAsync(500);

        await Assertions.Expect(_page.Locator(".dbt-toast").First).ToContainTextAsync("Failed");
        await Assertions.Expect(_page.Locator("[data-testid=rehearsal-readiness]").First).ToContainTextAsync("Failed");

        // The same failed/missing rehearsal must also block the campaign's completion gate (D11 -> D13/D14 integration).
        await GotoAsync($"/campaigns/{campaignId}/validation");
        await WaitForCircuitAsync();
        await Assertions.Expect(_page.Locator("[data-testid=validation-gate-banner]")).ToContainTextAsync("Not ready to complete");
        await Assertions.Expect(_page.Locator("[data-testid=validation-gate-reasons]")).ToContainTextAsync("Rehearsal evidence");
    }

    [Test]
    public async Task SchedulingAChangeWindow_DetectsAConflictWithAnotherScheduledWindow()
    {
        await LoginAsync("priya.nair@architonixlabs.com");
        var campaignId = await CreateCampaignAsync($"D12 change window campaign {Guid.NewGuid():N}"[..50], "AVALOKH-SALES-SQL");

        await GotoAsync($"/campaigns/{campaignId}/change-windows");
        await WaitForCircuitAsync();

        var start = DateTime.UtcNow.AddDays(5).Date.AddHours(2);
        var end = start.AddHours(2);

        await _page.FillAsync("input[placeholder='Title']", "First cutover window");
        await _page.Locator("input[type=datetime-local]").First.FillAsync(start.ToString("yyyy-MM-ddTHH:mm"));
        await _page.Locator("input[type=datetime-local]").Nth(1).FillAsync(end.ToString("yyyy-MM-ddTHH:mm"));
        await _page.Locator("[data-testid=create-window-button]").ClickAsync();
        await _page.WaitForTimeoutAsync(500);
        await Assertions.Expect(_page.Locator(".dbt-toast").First).ToContainTextAsync("planned");

        // Plan a second, overlapping window and check for a conflict before (and even after) creating it —
        // the check itself never blocks saving, since creating a plan must not execute or prevent anything by itself.
        await _page.FillAsync("input[placeholder='Title']", "Second overlapping window");
        await _page.Locator("input[type=datetime-local]").First.FillAsync(start.AddHours(1).ToString("yyyy-MM-ddTHH:mm"));
        await _page.Locator("input[type=datetime-local]").Nth(1).FillAsync(end.AddHours(1).ToString("yyyy-MM-ddTHH:mm"));
        await _page.Locator("[data-testid=check-conflicts-button]").ClickAsync();
        await _page.WaitForTimeoutAsync(500);

        var conflictResult = _page.Locator("[data-testid=conflict-result]");
        await Assertions.Expect(conflictResult).ToContainTextAsync("Freeze conflicts");
        await Assertions.Expect(conflictResult).ToContainTextAsync("First cutover window");

        // The conflict check never blocked saving the first window — exactly one exists so far, and
        // creating/checking the second hasn't added a row yet since only "Check conflicts" was clicked.
        await Assertions.Expect(_page.Locator("[data-testid=change-window-row]")).ToHaveCountAsync(1);
    }

    /// <summary>G03: a Planned window offers no way to Completed or RolledBack, and starting it still needs approvals.</summary>
    [Test]
    public async Task APlannedChangeWindow_CannotBeFinishedWithoutStarting_AndStartingNeedsApproval()
    {
        await LoginAsync("priya.nair@architonixlabs.com");
        var campaignId = await CreateCampaignAsync($"G03 window campaign {Guid.NewGuid():N}"[..50], "AVALOKH-SALES-SQL");

        await GotoAsync($"/campaigns/{campaignId}/change-windows");
        await WaitForCircuitAsync();
        var start = DateTime.UtcNow.AddDays(9).Date.AddHours(2);
        await _page.FillAsync("input[placeholder='Title']", "G03 window");
        await _page.Locator("input[type=datetime-local]").First.FillAsync(start.ToString("yyyy-MM-ddTHH:mm"));
        await _page.Locator("input[type=datetime-local]").Nth(1).FillAsync(start.AddHours(1).ToString("yyyy-MM-ddTHH:mm"));
        await _page.Locator("[data-testid=create-window-button]").ClickAsync();
        await _page.WaitForTimeoutAsync(500);

        await _page.Locator("[data-testid=change-window-row]").First.Locator("a:has-text('Open')").ClickAsync();
        await _page.WaitForURLAsync(url => url.Contains("/change-windows/"));
        await WaitForCircuitAsync();

        await Assertions.Expect(_page.Locator("[data-testid=status-Completed]")).ToHaveCountAsync(0);
        await Assertions.Expect(_page.Locator("[data-testid=status-RolledBack]")).ToHaveCountAsync(0);
        await Assertions.Expect(_page.Locator("[data-testid=status-Approved]")).ToBeVisibleAsync();

        // Starting without approvals or an emergency exception is refused, and the status is unchanged.
        await _page.Locator("[data-testid=status-InProgress]").ClickAsync();
        await _page.WaitForTimeoutAsync(500);
        await Assertions.Expect(_page.Locator("[data-testid=change-window-detail-status]")).ToHaveTextAsync("Planned");
    }

    [Test]
    public async Task CompletingACampaign_IsBlockedWithoutEvidence_ThenAllowedOnceValidationSignoffsAndRehearsalPass()
    {
        await LoginAsync("priya.nair@architonixlabs.com");
        var campaignId = await CreateCampaignAsync($"D13 completion gate campaign {Guid.NewGuid():N}"[..50], "AVALOKH-SALES-SQL");

        await GotoAsync($"/campaigns/{campaignId}");
        await WaitForCircuitAsync();

        await TransitionToAsync("Assessing", "Begin assessment");

        // Ready needs a current, passing rehearsal (performance included); Scheduled also needs an approval.
        await GotoAsync($"/campaigns/{campaignId}/rehearsals");
        await WaitForCircuitAsync();
        await _page.Locator("[data-testid=rehearsal-instance-select]").SelectOptionAsync(new SelectOptionValue { Label = "AVALOKH-SALES-SQL" });
        await _page.FillAsync("input[placeholder='Source label']", "SQL Server 2019");
        await _page.FillAsync("input[placeholder='Target label']", "SQL Server 2022");
        await _page.Locator("[data-testid=backup-restore-outcome]").SelectOptionAsync("Passed");
        await _page.Locator("[data-testid=correctness-outcome]").SelectOptionAsync("Passed");
        await _page.Locator("[data-testid=performance-outcome]").SelectOptionAsync("Passed");
        await _page.Locator("[data-testid=write-disposition]").SelectOptionAsync("NotApplicable");
        await _page.Locator("[data-testid=record-rehearsal-button]").ClickAsync();
        await _page.WaitForTimeoutAsync(500);

        await GotoAsync($"/campaigns/{campaignId}");
        await WaitForCircuitAsync();
        var approvalCard = _page.Locator(".dbt-card", new PageLocatorOptions { HasText = "Approval history" });
        await approvalCard.Locator("input[placeholder='Scope (e.g. DBA technical)']").FillAsync("DBA technical");
        await approvalCard.Locator("input[placeholder='Approver name']").FillAsync("Priya Nair");
        await approvalCard.GetByRole(AriaRole.Button, new LocatorGetByRoleOptions { Name = "Record approval" }).ClickAsync();
        await _page.WaitForTimeoutAsync(500);

        await TransitionToAsync("Ready", "Assessment complete");
        await TransitionToAsync("Scheduled", "Window scheduled");
        await TransitionToAsync("InProgress", "Cutover underway");
        await TransitionToAsync("Validating", "Cutover done, validating");

        // Blocked: no validation and no sign-offs yet.
        await GotoAsync($"/campaigns/{campaignId}");
        await WaitForCircuitAsync();
        await Assertions.Expect(_page.Locator("[data-testid=completion-gate-banner]")).ToContainTextAsync("blocked");

        var stateCard = _page.Locator(".dbt-card", new PageLocatorOptions { HasText = "State transition" });
        await stateCard.Locator("[data-testid=next-state-select]").SelectOptionAsync("Completed");
        await stateCard.Locator("input[placeholder='Reason']").FillAsync("Attempting completion");
        await stateCard.Locator("[data-testid=transition-button]").ClickAsync();
        await _page.WaitForTimeoutAsync(500);
        await Assertions.Expect(_page.Locator(".dbt-toast").First).ToContainTextAsync("rejected");
        await Assertions.Expect(_page.Locator("[data-testid=campaign-detail-state]")).ToContainTextAsync("Validating");

        // Supply a fully-passing validation and both required sign-offs.
        await GotoAsync($"/campaigns/{campaignId}/validation");
        await WaitForCircuitAsync();
        await _page.Locator("[data-testid=validation-instance-select]").SelectOptionAsync(new SelectOptionValue { Label = "AVALOKH-SALES-SQL" });
        await _page.FillAsync("input[placeholder='Observed target build']", "16.0.1000");
        await _page.Locator("[data-testid=app-tests-outcome]").SelectOptionAsync("Passed");
        await _page.Locator("[data-testid=integrity-outcome]").SelectOptionAsync("Passed");
        await _page.Locator("[data-testid=jobs-permissions-outcome]").SelectOptionAsync("Passed");
        await _page.Locator("[data-testid=ha-state-outcome]").SelectOptionAsync("Passed");
        await _page.Locator("[data-testid=backup-restore-evidence-outcome]").SelectOptionAsync("Passed");
        await _page.Locator("[data-testid=record-validation-button]").ClickAsync();
        await _page.WaitForTimeoutAsync(500);

        var validationRow = _page.Locator("[data-testid=validation-row]").First;
        await validationRow.Locator("select").First.SelectOptionAsync("DBA");
        await validationRow.Locator("input[placeholder='Reviewer name']").FillAsync("Priya Nair");
        await validationRow.Locator("[data-testid=add-signoff-button]").ClickAsync();
        await _page.WaitForTimeoutAsync(500);

        await validationRow.Locator("select").First.SelectOptionAsync("Application owner");
        await validationRow.Locator("input[placeholder='Reviewer name']").FillAsync("Dev Patel");
        await validationRow.Locator("[data-testid=add-signoff-button]").ClickAsync();
        await _page.WaitForTimeoutAsync(500);

        await Assertions.Expect(_page.Locator("[data-testid=validation-gate-banner]")).ToContainTextAsync("Ready to complete");

        // Now the same Completed transition succeeds.
        await GotoAsync($"/campaigns/{campaignId}");
        await WaitForCircuitAsync();
        await Assertions.Expect(_page.Locator("[data-testid=completion-gate-banner]")).ToContainTextAsync("ready");

        var stateCard2 = _page.Locator(".dbt-card", new PageLocatorOptions { HasText = "State transition" });
        await stateCard2.Locator("[data-testid=next-state-select]").SelectOptionAsync("Completed");
        await stateCard2.Locator("input[placeholder='Reason']").FillAsync("All evidence in place");
        await stateCard2.Locator("[data-testid=transition-button]").ClickAsync();
        await _page.WaitForTimeoutAsync(500);

        await Assertions.Expect(_page.Locator(".dbt-toast").First).ToContainTextAsync("Completed");
        await Assertions.Expect(_page.Locator("[data-testid=campaign-detail-state]")).ToContainTextAsync("Completed");
    }

    [Test]
    public async Task CreatingAnException_ThenRevokingIt_ImmediatelyReopensTheWaivedAction()
    {
        await LoginAsync("priya.nair@architonixlabs.com");

        await GotoAsync("/exceptions");
        await WaitForCircuitAsync();

        var reference = $"D11 rehearsal evidence {Guid.NewGuid():N}"[..40];
        await _page.FillAsync("input[placeholder='Target type (e.g. MigrationCampaign)']", "MigrationCampaign");
        await _page.FillAsync("input[placeholder='Target id']", "1");
        await _page.FillAsync("input[placeholder='Waived check/policy reference']", reference);
        await _page.FillAsync("textarea[placeholder='Reason']", "Emergency patch window — rehearsal could not be completed in time.");
        await _page.FillAsync("input[placeholder='Permitted approver role (e.g. Admin, ITManager)']", "DBA");
        await _page.Locator("#require-sep").UncheckAsync(); // same user approves in this test
        await _page.Locator("[data-testid=exception-expires-input]").FillAsync(DateTime.UtcNow.AddDays(10).ToString("yyyy-MM-dd"));
        await _page.Locator("[data-testid=create-exception-button]").ClickAsync();
        await _page.WaitForTimeoutAsync(500);
        await Assertions.Expect(_page.Locator(".dbt-toast").First).ToContainTextAsync("pending approval");

        var row = _page.Locator("[data-testid=exception-row]", new PageLocatorOptions { HasText = reference });
        await Assertions.Expect(row.Locator("[data-testid=exception-status]")).ToContainTextAsync("PendingApproval");

        await row.Locator("[data-testid=approve-exception-button]").ClickAsync();
        await _page.WaitForTimeoutAsync(500);
        await Assertions.Expect(_page.Locator(".dbt-toast").First).ToContainTextAsync("approved");
        await Assertions.Expect(row.Locator("[data-testid=exception-status]")).ToContainTextAsync("Active");

        // Revoking reopens the waived action immediately — no sweep/delay required.
        await row.Locator("[data-testid=revoke-exception-button]").ClickAsync();
        await _page.WaitForTimeoutAsync(500);
        await Assertions.Expect(_page.Locator(".dbt-toast").First).ToContainTextAsync("reopened");
        await Assertions.Expect(row.Locator("[data-testid=exception-status]")).ToContainTextAsync("Revoked");

        // History is preserved — renewing creates a new linked record rather than editing the old one.
        await row.Locator("[data-testid=renew-exception-button]").ClickAsync();
        await _page.WaitForTimeoutAsync(500);
        await Assertions.Expect(_page.Locator(".dbt-toast").First).ToContainTextAsync("Renewal");
        await Assertions.Expect(_page.Locator("[data-testid=exception-row]", new PageLocatorOptions { HasText = reference })).ToHaveCountAsync(2);
    }

    [Test]
    public async Task LifecycleWorkspace_ShowsFilteredDenominatorLabelledView_AndExportsTheSameScopedRows()
    {
        await LoginAsync("angela.price@contoso.com");

        await GotoAsync("/lifecycle/workspace");
        await WaitForCircuitAsync();

        var summary = _page.Locator("[data-testid=workspace-summary]");
        await Assertions.Expect(summary).ToBeVisibleAsync();
        await Assertions.Expect(_page.Locator("[data-testid=workspace-denominator]")).Not.ToHaveTextAsync("0");

        var totalRowsBefore = await _page.Locator("[data-testid=workspace-row]").CountAsync();

        // Apply an engine filter and confirm the matching count/table narrow together.
        await _page.Locator("select").First.SelectOptionAsync("MySql");
        await _page.WaitForTimeoutAsync(400);

        var filteredRows = await _page.Locator("[data-testid=workspace-row]").CountAsync();
        Assert.That(filteredRows, Is.LessThanOrEqualTo(totalRowsBefore));

        var matchingText = await _page.Locator("[data-testid=workspace-matching]").InnerTextAsync();
        Assert.That(int.Parse(matchingText), Is.EqualTo(filteredRows));

        // Export downloads exactly the filtered (not the full) scoped result set.
        var downloadTask = _page.WaitForDownloadAsync();
        await _page.Locator("[data-testid=export-workspace-button]").ClickAsync();
        var download = await downloadTask;
        Assert.That(download.SuggestedFilename, Does.StartWith("dbtracker-lifecycle-workspace-"));
        await Assertions.Expect(_page.Locator(".dbt-toast").First).ToContainTextAsync($"Exported {filteredRows}");
    }

    private async Task TransitionToAsync(string toState, string reason)
    {
        var stateCard = _page.Locator(".dbt-card", new PageLocatorOptions { HasText = "State transition" });
        await stateCard.Locator("[data-testid=next-state-select]").SelectOptionAsync(toState);
        await stateCard.Locator("input[placeholder='Reason']").FillAsync(reason);
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

        var match = Regex.Match(_page.Url, @"/campaigns/(\d+)");
        return int.Parse(match.Groups[1].Value);
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
