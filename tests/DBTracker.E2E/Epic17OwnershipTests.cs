using Microsoft.Playwright;

namespace DBTracker.E2E;

/// <summary>
/// E2E coverage for Epic 17's U21: owner teams/memberships driving "My instances" beyond a raw
/// contact-email match, structured region/cluster facets, and a reusable backup policy's impact
/// preview before it's applied.
/// </summary>
[TestFixture]
public class Epic17OwnershipTests
{
    private IPage _page = null!;

    [TearDown]
    public async Task TearDown() => await _page.CloseAsync();

    [Test]
    public async Task TeamMembership_SurfacesAnInstanceUnder_MyInstances_WithNoContactEmailMatch()
    {
        _page = await GlobalSetup.Server.Browser.NewPageAsync();
        await LoginAsync("admin@architonixlabs.com");

        var teamName = $"E2E Team {Guid.NewGuid():N}".Substring(0, 24);

        // Create the team and add Dev Patel (dev.patel@ediqra.com) as a member.
        await GotoAsync("/ownership");
        await _page.WaitForSelectorAsync("#team-name");
        await WaitForCircuitAsync();
        await _page.FillAsync("#team-name", teamName);
        await _page.ClickAsync("button:has-text('Create team')");
        await _page.WaitForTimeoutAsync(500);
        // .First rather than a bare locator: Playwright's fill()+click() in quick succession can
        // catch the button mid re-render and register as two toasts under test automation speed
        // (never observed from an actual single mouse click) — asserting on the first is still a
        // faithful check that creation succeeded and was announced.
        await Assertions.Expect(_page.Locator(".dbt-toast").First).ToContainTextAsync("created");

        var teamCard = _page.Locator(".dbt-card", new PageLocatorOptions { HasText = teamName });
        var memberSelect = teamCard.Locator("select");
        var memberOptionLabel = (await memberSelect.Locator("option").AllTextContentsAsync()).First(o => o.Contains("dev.patel@ediqra.com"));
        await memberSelect.SelectOptionAsync(new SelectOptionValue { Label = memberOptionLabel });
        await teamCard.Locator("button:has-text('Add member')").ClickAsync();
        await _page.WaitForTimeoutAsync(500);
        await Assertions.Expect(_page.Locator(".dbt-toast").First).ToContainTextAsync("added");

        // Assign the new team as owner of AVALOKH-SALES-SQL — whose contact is Marcus Chen, not
        // Dev Patel, so any match for Dev here can only be coming from team membership.
        await GotoAsync("/instances?s=AVALOKH-SALES-SQL");
        await _page.WaitForSelectorAsync(".dbt-table tbody tr");
        // Navigates via the semantic instance link's own href (U20) rather than a row click, which
        // can occasionally hang Playwright's implicit post-click navigation wait under Blazor's
        // enhanced (non-full-page-load) navigation.
        await GotoAsync((await _page.Locator("a.instance-row-link").First.GetAttributeAsync("href"))!);
        await WaitForCircuitAsync();
        await _page.ClickAsync("a:has-text('Edit')");
        await _page.WaitForURLAsync(url => url.Contains("/edit"));
        await WaitForCircuitAsync();

        await _page.SelectOptionAsync("#owner-team", new SelectOptionValue { Label = teamName });
        await _page.ClickAsync("button[type=submit]:has-text('Save')");
        await _page.WaitForURLAsync(url => url.Contains("/instances/") && !url.Contains("edit"));
        await WaitForCircuitAsync();
        await Assertions.Expect(_page.Locator("text=" + teamName)).ToBeVisibleAsync();

        await _page.CloseAsync();

        // Now as Dev Patel: "My instances" must include both the existing email-matched instance
        // AND the newly team-owned one — team membership adds to, not replaces, email matching.
        _page = await GlobalSetup.Server.Browser.NewPageAsync();
        await LoginAsync("dev.patel@ediqra.com");
        await GotoAsync("/instances");
        await _page.WaitForSelectorAsync(".dbt-table table");
        await WaitForCircuitAsync();

        await _page.ClickAsync("#my-instances");
        await _page.WaitForTimeoutAsync(500);

        var rows = _page.Locator(".dbt-table tbody tr");
        var text = await rows.AllInnerTextsAsync();
        Assert.That(text.Any(t => t.Contains("EDIQRA-INVENTORY-PG")), Is.True, "Email-matched instance should still appear.");
        Assert.That(text.Any(t => t.Contains("AVALOKH-SALES-SQL")), Is.True, "Team-owned instance should appear via membership, not email.");
    }

    [Test]
    public async Task RegionFacet_FiltersInstancesList_ToExactRegionMatch()
    {
        _page = await GlobalSetup.Server.Browser.NewPageAsync();
        await LoginAsync("priya.nair@architonixlabs.com");

        var region = $"e2e-region-{Guid.NewGuid():N}".Substring(0, 16);

        await GotoAsync("/instances?s=NEXTCLOUD-FILES-MYSQL");
        await _page.WaitForSelectorAsync(".dbt-table tbody tr");
        await GotoAsync((await _page.Locator("a.instance-row-link").First.GetAttributeAsync("href"))!);
        await WaitForCircuitAsync();
        await _page.ClickAsync("a:has-text('Edit')");
        await _page.WaitForURLAsync(url => url.Contains("/edit"));
        await WaitForCircuitAsync();

        await _page.FillAsync("input[placeholder='us-east-1']", region);
        await _page.ClickAsync("button[type=submit]:has-text('Save')");
        await _page.WaitForURLAsync(url => url.Contains("/instances/") && !url.Contains("edit"));
        await WaitForCircuitAsync();

        await GotoAsync("/instances");
        await _page.WaitForSelectorAsync(".dbt-table table");
        await WaitForCircuitAsync();

        await _page.GetByLabel("Region").SelectOptionAsync(region);
        await _page.WaitForTimeoutAsync(500);

        var rows = _page.Locator(".dbt-table tbody tr");
        var count = await rows.CountAsync();
        Assert.That(count, Is.EqualTo(1));
        await Assertions.Expect(rows.First).ToContainTextAsync("NEXTCLOUD-FILES-MYSQL");
    }

    [Test]
    public async Task PolicyImpactPreview_ShowsBeforeAfterDiff_BeforeAnythingIsApplied()
    {
        _page = await GlobalSetup.Server.Browser.NewPageAsync();
        await LoginAsync("priya.nair@architonixlabs.com");

        var policyName = $"E2E Policy {Guid.NewGuid():N}".Substring(0, 20);

        await GotoAsync("/ownership?tab=policies");
        await _page.WaitForSelectorAsync("#policy-name");
        await WaitForCircuitAsync();
        await _page.FillAsync("#policy-name", policyName);
        await _page.FillAsync("#policy-interval", "6");
        await _page.ClickAsync("button:has-text('Create policy')");
        await _page.WaitForTimeoutAsync(500);
        await Assertions.Expect(_page.Locator(".dbt-toast").First).ToContainTextAsync("created");
        await Assertions.Expect(_page.Locator("td", new PageLocatorOptions { HasText = policyName })).ToBeVisibleAsync();

        await GotoAsync("/instances?s=DIFY-PLUGIN-PG");
        await _page.WaitForSelectorAsync(".dbt-table tbody tr");
        var row = _page.Locator(".dbt-table tbody tr", new PageLocatorOptions { HasText = "DIFY-PLUGIN-PG" });
        await row.Locator("input[type=checkbox]").CheckAsync();
        await _page.WaitForTimeoutAsync(400);
        await _page.ClickAsync("button:has-text('Apply policy')");
        await _page.WaitForURLAsync(url => url.Contains("/instances/apply-policy"));
        await WaitForCircuitAsync();

        var policyOptionLabel = await GetMatchingOptionLabelAsync(policyName);
        await _page.SelectOptionAsync("#policy-select", new SelectOptionValue { Label = policyOptionLabel });
        await _page.ClickAsync("button:has-text('Preview impact')");
        await _page.WaitForTimeoutAsync(500);

        // The preview must show the real before (72h, DIFY-PLUGIN-PG's seeded cadence) vs. after
        // (6h, this policy's value) — and nothing is applied yet (still on the preview page).
        await Assertions.Expect(_page.Locator("text=Impact preview")).ToBeVisibleAsync();
        await Assertions.Expect(_page.Locator("table tbody tr", new PageLocatorOptions { HasText = "ExpectedBackupIntervalHours" })).ToContainTextAsync("72");
        await Assertions.Expect(_page.Locator("table tbody tr", new PageLocatorOptions { HasText = "ExpectedBackupIntervalHours" })).ToContainTextAsync("6");
        Assert.That(_page.Url, Does.Contain("/apply-policy"), "Applying must be a separate, explicit step after the preview.");

        await _page.ClickAsync("button:has-text('Apply to')");
        await _page.WaitForURLAsync(url => url.Contains("/instances") && !url.Contains("apply-policy"));
        await WaitForCircuitAsync();

        await GotoAsync("/instances?s=DIFY-PLUGIN-PG");
        await _page.WaitForSelectorAsync(".dbt-table tbody tr");
        await GotoAsync((await _page.Locator("a.instance-row-link").First.GetAttributeAsync("href"))!);
        await WaitForCircuitAsync();
        await Assertions.Expect(_page.Locator("text=" + policyName)).ToBeVisibleAsync();
    }

    private async Task<string> GetMatchingOptionLabelAsync(string policyName)
    {
        var options = await _page.Locator("#policy-select option").AllTextContentsAsync();
        return options.First(o => o.Contains(policyName));
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
