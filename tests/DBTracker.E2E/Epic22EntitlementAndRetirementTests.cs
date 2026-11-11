using System.Text.RegularExpressions;
using Microsoft.Playwright;

namespace DBTracker.E2E;

/// <summary>
/// E2E coverage for Epic 22 (D21-D22): support entitlement/edition-cost readiness and the
/// decommission/source-retirement checklist. D22's completion path is required to call the
/// pre-existing U14 InstanceArchiveService rather than invent a second archive/delete mechanism —
/// the last test proves that by checking the instance lands in the same archived-instances browser
/// U14 already built, not just that some "retired" flag flipped somewhere.
/// </summary>
[TestFixture]
public class Epic22EntitlementAndRetirementTests
{
    private IPage _page = null!;

    [TearDown]
    public async Task TearDown() => await _page.CloseAsync();

    private async Task LoginAsync(string email)
    {
        if (_page is not null)
        {
            await _page.CloseAsync();
        }
        _page = await GlobalSetup.Server.Browser.NewPageAsync();
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

    private async Task<int> CreateInstanceAsync(string instanceName)
    {
        await GotoAsync("/instances/new");
        await _page.WaitForSelectorAsync("button[type=submit]", new PageWaitForSelectorOptions { Timeout = 60000 });
        await WaitForCircuitAsync();
        await FillByLabel("Instance name", instanceName);
        await SelectByLabel("Engine", "PostgreSql");
        await FillByLabel("Application", "E2E Epic 22");
        await FillByLabel("Company", "Architonix Labs");
        await FillByLabel("Contact email", "qa@architonixlabs.com");

        await _page.ClickAsync("button[type=submit]:text('Save')");
        await _page.WaitForURLAsync(url => url.Contains("/instances/"));
        await WaitForCircuitAsync();

        var match = Regex.Match(_page.Url, @"/instances/(\d+)");
        return int.Parse(match.Groups[1].Value);
    }

    private async Task FillByLabel(string label, string value)
    {
        var input = _page.Locator($"label:text-is('{label}') + input, label:text-is('{label}') ~ input").First;
        await input.FillAsync(value);
    }

    private async Task SelectByLabel(string label, string optionText)
    {
        var select = _page.Locator($"label:text-is('{label}') + select").First;
        await select.SelectOptionAsync(new SelectOptionValue { Label = optionText });
    }

    [Test]
    public async Task SupportContract_ExpiredEntitlement_FlaggedSeparatelyFromProductLifecycle()
    {
        // Instance creation is Roles.CanEditPolicy (Admin/DBA) only; entitlement linking below is
        // Roles.CommercialEvidencePolicy (Admin/ITManager) — switch to ITManager for that part.
        await LoginAsync("admin@architonixlabs.com");
        var instanceId = await CreateInstanceAsync($"E2E-ENTITLE-{Guid.NewGuid():N}"[..24].ToUpperInvariant());

        await LoginAsync("marcus.chen@architonixlabs.com"); // ITManager — Roles.CommercialEvidencePolicy
        await GotoAsync($"/instances/{instanceId}/entitlement");
        await WaitForCircuitAsync();

        // Before linking anything: a distinct "no contract" status, never confused with lifecycle.
        await Assertions.Expect(_page.Locator("[data-testid=entitlement-status]")).ToContainTextAsync("NoContractOnFile");
        await Assertions.Expect(_page.Locator("body")).ToContainTextAsync("Product lifecycle / support phase");

        var expiredEnd = DateTime.UtcNow.AddDays(-10).ToString("yyyy-MM-dd");
        var expiredStart = DateTime.UtcNow.AddYears(-2).ToString("yyyy-MM-dd");

        await _page.Locator("input[placeholder='Vendor']").FillAsync("Acme Support Co (E2E)");
        var dateInputs = _page.Locator("input[type='date']");
        await dateInputs.Nth(0).FillAsync(expiredStart);
        await dateInputs.Nth(1).FillAsync(expiredEnd);
        await _page.Locator("[data-testid=link-support-contract]").ClickAsync();
        await _page.WaitForTimeoutAsync(600);

        await Assertions.Expect(_page.Locator(".dbt-toast").First).ToContainTextAsync("linked");
        await Assertions.Expect(_page.Locator("[data-testid=entitlement-status]")).ToContainTextAsync("Expired");
        await Assertions.Expect(_page.Locator("[data-testid=support-contract-row]")).ToContainTextAsync("Acme Support Co (E2E)");

        // The entitlement status and the product-lifecycle link are shown as two separate facts on
        // this one page, never derived from each other — the lifecycle page is a separate link, not
        // a value this status was computed from.
        await Assertions.Expect(_page.Locator("[data-testid=entitlement-detail]")).ToContainTextAsync("expired");
    }

    [Test]
    public async Task CostEstimate_VisibleToItManager_MaskedForViewer()
    {
        await LoginAsync("admin@architonixlabs.com");
        var instanceId = await CreateInstanceAsync($"E2E-COST-{Guid.NewGuid():N}"[..24].ToUpperInvariant());

        await LoginAsync("marcus.chen@architonixlabs.com"); // ITManager
        await GotoAsync($"/instances/{instanceId}/entitlement");
        await WaitForCircuitAsync();

        await _page.Locator("input[placeholder='Target edition/path']").FillAsync("SQL Server 2022 Enterprise (E2E)");
        await _page.Locator("[data-testid=cost-amount-input]").FillAsync("123456.78");
        await _page.Locator("[data-testid=add-cost-estimate]").ClickAsync();
        await _page.WaitForTimeoutAsync(600);

        await Assertions.Expect(_page.Locator(".dbt-toast").First).ToContainTextAsync("recorded");
        await Assertions.Expect(_page.Locator("[data-testid=cost-estimate-amount]")).ToContainTextAsync("123,456.78");

        // Same page, as the Viewer persona: the real figure must never appear anywhere on the page.
        await LoginAsync("dev.patel@ediqra.com"); // Viewer
        await GotoAsync($"/instances/{instanceId}/entitlement");
        await WaitForCircuitAsync();

        await Assertions.Expect(_page.Locator("[data-testid=cost-estimate-row]")).ToContainTextAsync("SQL Server 2022 Enterprise (E2E)");
        await Assertions.Expect(_page.Locator("[data-testid=cost-estimate-amount]")).ToContainTextAsync("masked");
        await Assertions.Expect(_page.Locator("body")).Not.ToContainTextAsync("123,456.78");
        // A Viewer also can't record a new estimate — the commercial-evidence form is gone entirely.
        await Assertions.Expect(_page.Locator("[data-testid=add-cost-estimate]")).Not.ToBeVisibleAsync();

        // G06: the auditor sees that an estimate was recorded, but not the figure, and can't find it by searching for it.
        await LoginAsync("angela.price@contoso.com"); // Auditor
        await GotoAsync("/audit");
        await _page.WaitForSelectorAsync("tbody tr");
        await Assertions.Expect(_page.Locator("tbody tr", new PageLocatorOptions { HasText = "EditionCostEstimateRecorded" }).First).ToBeVisibleAsync();
        await Assertions.Expect(_page.Locator("body")).Not.ToContainTextAsync("123,456.78");
        await WaitForCircuitAsync();
        await _page.FillAsync("#audit-search", "123,456.78");
        await _page.WaitForTimeoutAsync(900);
        await Assertions.Expect(_page.Locator("tbody tr", new PageLocatorOptions { HasText = "EditionCostEstimateRecorded" })).ToHaveCountAsync(0);
    }

    /// <summary>G06: a DBA records contract facts (vendor, coverage, dates), which carry no money; cost estimates stay Admin/IT Manager.</summary>
    [Test]
    public async Task Dba_CanLinkASupportContract_ButNotRecordACostEstimate()
    {
        await LoginAsync("priya.nair@architonixlabs.com"); // DBA
        var instanceId = await CreateInstanceAsync($"E2E-CONTRACT-{Guid.NewGuid():N}"[..24].ToUpperInvariant());
        await GotoAsync($"/instances/{instanceId}/entitlement");
        await WaitForCircuitAsync();

        await Assertions.Expect(_page.Locator("#contract-vendor")).ToBeVisibleAsync();
        await Assertions.Expect(_page.Locator("[data-testid=add-cost-estimate]")).Not.ToBeVisibleAsync();
    }

    [Test]
    public async Task RetirementChecklist_BlockedOnDependency_ThenExceptionBypasses_ThenCompletesViaExistingArchiveFlow()
    {
        await LoginAsync("admin@architonixlabs.com"); // Admin — CanEditPolicy + CommercialEvidencePolicy + exception approval

        var instanceName = $"E2E-RETIRE-{Guid.NewGuid():N}"[..24].ToUpperInvariant();
        var instanceId = await CreateInstanceAsync(instanceName);

        // Record one unresolved downstream-consumer dependency (D07) on the Migration page.
        await GotoAsync($"/instances/{instanceId}/migration");
        await WaitForCircuitAsync();
        var depCard = _page.Locator(".dbt-card", new PageLocatorOptions { HasText = "Technical dependencies" });
        await depCard.Locator("select").Nth(1).SelectOptionAsync("Downstream"); // _depDirection
        await depCard.Locator("input[placeholder='Name']").FillAsync("BillingApp (E2E)");
        await depCard.GetByRole(AriaRole.Button, new LocatorGetByRoleOptions { Name = "Record dependency" }).ClickAsync();
        await _page.WaitForTimeoutAsync(600);

        // Viewing doesn't create a checklist; starting one is explicit. It starts blocked on that unresolved consumer.
        await GotoAsync($"/instances/{instanceId}/retirement");
        await WaitForCircuitAsync();
        await _page.Locator("[data-testid=start-retirement-checklist]").ClickAsync();
        var consumerRow = _page.Locator("[data-testid=retirement-gate-row]", new PageLocatorOptions { HasText = "Downstream consumers" });
        await Assertions.Expect(consumerRow.Locator("[data-testid=retirement-gate-status]")).ToContainTextAsync("Blocked");
        await Assertions.Expect(_page.Locator("[data-testid=retirement-overall-status]")).ToContainTextAsync("Blocked");
        await Assertions.Expect(_page.Locator("[data-testid=complete-retirement-button]")).ToBeDisabledAsync();

        var checklistHeading = await _page.Locator("h1").InnerTextAsync();
        var checklistId = int.Parse(Regex.Match(checklistHeading, @"#(\d+)").Groups[1].Value);

        // Resolve the dependency — that one gate now passes, but the checklist overall is still
        // blocked by every other (still-Unknown) gate, so retirement remains unavailable without a
        // scoped exception.
        await GotoAsync($"/instances/{instanceId}/migration");
        await WaitForCircuitAsync();
        await _page.Locator("button", new PageLocatorOptions { HasText = "Mark resolved" }).First.ClickAsync();
        await _page.WaitForTimeoutAsync(600);

        await GotoAsync($"/instances/{instanceId}/retirement");
        await WaitForCircuitAsync();
        var consumerRowAfter = _page.Locator("[data-testid=retirement-gate-row]", new PageLocatorOptions { HasText = "Downstream consumers" });
        await Assertions.Expect(consumerRowAfter.Locator("[data-testid=retirement-gate-status]")).ToContainTextAsync("Passed");
        await Assertions.Expect(_page.Locator("[data-testid=retirement-overall-status]")).ToContainTextAsync("Blocked");
        await Assertions.Expect(_page.Locator("[data-testid=complete-retirement-button]")).ToBeDisabledAsync();

        // Record and approve a scoped D14 exception targeting this exact checklist.
        await GotoAsync("/exceptions");
        await WaitForCircuitAsync();
        await _page.Locator("input[placeholder='Target type (e.g. MigrationCampaign)']").FillAsync("RetirementChecklist");
        await _page.Locator("input[type=number]").First.FillAsync(checklistId.ToString());
        await _page.Locator("input[placeholder='Waived check/policy reference']").FillAsync("D22 retirement checklist (E2E)");
        await _page.Locator("textarea[placeholder='Reason']").FillAsync("E2E scoped bypass — remaining manual gates not completed in this test.");
        // Same Admin user both prepares and approves this exception below — uncheck "requires
        // separate approver" (the form's default) or DecideAsync correctly refuses the approval.
        await _page.Locator("#require-sep").UncheckAsync();
        await _page.Locator("[data-testid=exception-expires-input]").FillAsync(DateTime.UtcNow.AddDays(1).ToString("yyyy-MM-dd"));
        await _page.Locator("[data-testid=create-exception-button]").ClickAsync();
        await _page.WaitForTimeoutAsync(600);

        var exceptionRow = _page.Locator("[data-testid=exception-row]", new PageLocatorOptions { HasText = "RetirementChecklist" });
        await Assertions.Expect(exceptionRow.Locator("[data-testid=exception-status]")).ToContainTextAsync("PendingApproval");
        await exceptionRow.Locator("[data-testid=approve-exception-button]").ClickAsync();
        await _page.WaitForTimeoutAsync(600);
        // The strongest proof of a successful approval is the exception's own status column —
        // toast ordering/stacking isn't asserted on here since it isn't the point of this test.
        await Assertions.Expect(exceptionRow.Locator("[data-testid=exception-status]")).ToContainTextAsync("Active");

        // Back on the checklist: still Blocked on the merits, but a scoped exception now makes
        // retirement available.
        await GotoAsync($"/instances/{instanceId}/retirement");
        await WaitForCircuitAsync();
        await Assertions.Expect(_page.Locator("[data-testid=retirement-overall-status]")).ToContainTextAsync("scoped exception is Active");

        // The button is still gated on a non-empty reason, independent of CanRetire — fill it first.
        await _page.Locator("[data-testid=retirement-reason-input]").FillAsync("Replaced by new platform (E2E) — retiring under scoped exception.");
        await Assertions.Expect(_page.Locator("[data-testid=complete-retirement-button]")).ToBeEnabledAsync();
        await _page.Locator("[data-testid=complete-retirement-button]").ClickAsync();
        await _page.WaitForTimeoutAsync(800);

        // Completing retirement must land back on the instance detail page showing the *existing*
        // U14 archived banner — proof this reused that mechanism rather than inventing a new one.
        await _page.WaitForURLAsync(url => url.Contains($"/instances/{instanceId}") && !url.Contains("retirement"));
        await WaitForCircuitAsync();
        await Assertions.Expect(_page.Locator("body")).ToContainTextAsync("This instance is archived");
        await Assertions.Expect(_page.Locator("body")).ToContainTextAsync("Replaced by new platform (E2E)");

        // And it must show up in U14's own pre-existing archived-instances browser.
        await GotoAsync("/instances/archived");
        await WaitForCircuitAsync();
        await Assertions.Expect(_page.Locator("tbody tr", new PageLocatorOptions { HasText = instanceName }).First).ToBeVisibleAsync();
    }

    /// <summary>G01: while a retirement checklist is open, a plain archive is refused, so the checklist's gates can't be
    /// skipped. Cancelling the checklist (with a reason) restores the plain archive for a genuine duplicate.</summary>
    [Test]
    public async Task PlainArchive_RefusedWhileChecklistOpen_AllowedAfterCancel()
    {
        await LoginAsync("admin@architonixlabs.com");
        var instanceName = $"E2E-G01-{Guid.NewGuid():N}"[..24].ToUpperInvariant();
        var instanceId = await CreateInstanceAsync(instanceName);

        // Just viewing the retirement page leaves no checklist behind.
        await GotoAsync($"/instances/{instanceId}/retirement");
        await WaitForCircuitAsync();
        await Assertions.Expect(_page.Locator("[data-testid=start-retirement-checklist]")).ToBeVisibleAsync();
        await _page.Locator("[data-testid=start-retirement-checklist]").ClickAsync();
        await Assertions.Expect(_page.Locator("[data-testid=retirement-gate-table]")).ToBeVisibleAsync();

        // Plain archive is refused with the checklist named.
        await GotoAsync($"/instances/{instanceId}");
        await WaitForCircuitAsync();
        await _page.ClickAsync("button:has-text('Archive')");
        await _page.ClickAsync("[data-testid=archive-kind-mistake]");
        await _page.FillAsync("#archive-reason", "E2E duplicate entry");
        await _page.ClickAsync("button:has-text('Archive this instance')");
        await Assertions.Expect(_page.Locator("[role=alert]", new PageLocatorOptions { HasText = "Retirement checklist" })).ToBeVisibleAsync();
        Assert.That(_page.Url, Does.Contain($"/instances/{instanceId}"), "A refused archive must stay on the instance page.");

        // Cancel the checklist; the instance stays active and the page offers a fresh start.
        await GotoAsync($"/instances/{instanceId}/retirement");
        await WaitForCircuitAsync();
        await _page.Locator("[data-testid=cancel-checklist-reason]").FillAsync("Started by mistake (E2E)");
        await _page.Locator("[data-testid=cancel-checklist-button]").ClickAsync();
        await Assertions.Expect(_page.Locator("[data-testid=start-retirement-checklist]")).ToBeVisibleAsync();

        // Now the duplicate can be archived directly.
        await GotoAsync($"/instances/{instanceId}");
        await WaitForCircuitAsync();
        await _page.ClickAsync("button:has-text('Archive')");
        await _page.ClickAsync("[data-testid=archive-kind-mistake]");
        await _page.FillAsync("#archive-reason", "E2E duplicate entry");
        await _page.ClickAsync("button:has-text('Archive this instance')");
        await _page.WaitForURLAsync(url => url.EndsWith("/instances") || url.Contains("/instances?"));

        // The audit trail says which kind of archive it was.
        await GotoAsync("/audit");
        await WaitForCircuitAsync();
        var auditBody = await _page.Locator("body").InnerTextAsync();
        Assert.That(auditBody, Does.Contain("Not a retirement"));
        Assert.That(auditBody, Does.Contain("RetirementChecklistCancelled"));
    }
}
