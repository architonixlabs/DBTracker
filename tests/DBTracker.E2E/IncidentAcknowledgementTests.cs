using Microsoft.Playwright;

namespace DBTracker.E2E;

/// <summary>E2E coverage for Epic 14's U08: acknowledging an incident is a response-tracking overlay that never changes the computed health badge.</summary>
[TestFixture]
public class IncidentAcknowledgementTests
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
    public async Task AcknowledgingTheSeededIncident_StopsEscalationNote_ButHealthBadgeStaysFailed()
    {
        await GotoAsync("/instances?s=CONTOSO-HR-MYSQL");
        await _page.WaitForSelectorAsync(".dbt-table tbody tr");
        await WaitForCircuitAsync();
        await _page.ClickAsync(".dbt-table tbody tr");
        await _page.WaitForURLAsync(url => url.Contains("/instances/"));
        await WaitForCircuitAsync();

        var healthBefore = await _page.Locator("h1").InnerTextAsync();
        Assert.That(healthBefore, Does.Contain("Failed"));

        var incidentCard = _page.Locator(".dbt-card", new PageLocatorOptions { HasText = "Open incident" });
        await Assertions.Expect(incidentCard).ToBeVisibleAsync();
        await Assertions.Expect(incidentCard).ToContainTextAsync("Unacknowledged");

        await incidentCard.Locator("input").Nth(1).FillAsync("Investigating disk space issue");
        await incidentCard.Locator("input").Nth(2).FillAsync("TICKET-4521");
        await incidentCard.GetByRole(AriaRole.Button, new LocatorGetByRoleOptions { Name = "Acknowledge" }).ClickAsync();
        await _page.WaitForTimeoutAsync(800);

        await Assertions.Expect(_page.Locator(".dbt-toast")).ToContainTextAsync("acknowledged");

        var incidentCardAfter = _page.Locator(".dbt-card", new PageLocatorOptions { HasText = "Open incident" });
        await Assertions.Expect(incidentCardAfter).ToContainTextAsync("Acknowledged");
        await Assertions.Expect(incidentCardAfter).ToContainTextAsync("never changes the health badge");

        var healthAfter = await _page.Locator("h1").InnerTextAsync();
        Assert.That(healthAfter, Does.Contain("Failed"), "Acknowledging an incident must never change the computed health.");
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
        _page.GotoAsync(TestServerFixture.BaseUrl + path, new PageGotoOptions { WaitUntil = WaitUntilState.NetworkIdle });

    private Task WaitForCircuitAsync() => _page.WaitForTimeoutAsync(800);
}
