using DBTracker.Core.Entities;
using DBTracker.Infrastructure;
using Microsoft.EntityFrameworkCore;
using Microsoft.Playwright;

namespace DBTracker.E2E;

/// <summary>Epic 25 G09/G10: scan-attention links, Reports URLs, saved-view rename, the bell's pause control, team
/// subscriptions, quiet hours, the /setup delivery history and audited incident actions.</summary>
[TestFixture]
public class MonitoringAndDeliveryGapTests
{
    private const string TestConnectionString =
        "Server=localhost;Database=DBTracker_Test;Integrated Security=True;TrustServerCertificate=True;";

    private IPage _page = null!;

    [SetUp]
    public async Task SetUp() => _page = await GlobalSetup.Server.Browser.NewPageAsync();

    [TearDown]
    public async Task TearDown() => await _page.CloseAsync();

    [Test]
    public async Task ScanBanner_LinksToOnlyTheRowsNeedingAttention()
    {
        await LoginAsync("priya.nair@architonixlabs.com");
        await GotoAsync("/");
        await _page.WaitForSelectorAsync("[data-testid=scan-issue-banner]");

        // The seeded CONTOSO-HR-MYSQL scan errors, so the banner names it.
        await Assertions.Expect(_page.Locator("[data-testid=scan-issue-banner]")).ToContainTextAsync("CONTOSO-HR-MYSQL");

        await _page.ClickAsync("a:has-text('View scan activity')");
        await _page.WaitForURLAsync(url => url.Contains("scan=attention"));
        await WaitForCircuitAsync();

        await Assertions.Expect(_page.Locator("[data-testid=scan-filter-attention]")).ToHaveAttributeAsync("aria-pressed", "true");
        var rows = _page.Locator("[data-testid=scan-activity-row]");
        await Assertions.Expect(rows.Filter(new LocatorFilterOptions { HasText = "CONTOSO-HR-MYSQL" })).ToBeVisibleAsync();
        // A seeded instance whose last scan succeeded is not in the attention list.
        await Assertions.Expect(rows.Filter(new LocatorFilterOptions { HasText = "ARXMAIL-PROD-01" })).ToHaveCountAsync(0);
        // The error row keeps its last success separately from the failed attempt.
        await Assertions.Expect(rows.Filter(new LocatorFilterOptions { HasText = "CONTOSO-HR-MYSQL" })).ToContainTextAsync("last successful scan was");

        await _page.ClickAsync("[data-testid=scan-filter-all]");
        await WaitForCircuitAsync();
        await Assertions.Expect(rows.Filter(new LocatorFilterOptions { HasText = "ARXMAIL-PROD-01" })).ToBeVisibleAsync();
        // The test configuration turns background scanning off, which shows as its own state, not Healthy.
        await Assertions.Expect(_page.Locator("[data-testid=scan-background-off]")).ToBeVisibleAsync();
    }

    [Test]
    public async Task ReportsDateRange_IsInTheUrl_AndSurvivesReload()
    {
        await LoginAsync("priya.nair@architonixlabs.com");
        await GotoAsync("/reports?type=BackupActivity&from=2026-01-01&to=2026-01-31");
        await _page.WaitForSelectorAsync(".stat-strip");
        await WaitForCircuitAsync();

        await Assertions.Expect(_page.Locator("[data-testid=reports-range-custom]")).ToHaveAttributeAsync("aria-pressed", "true");
        await Assertions.Expect(_page.Locator("#reports-from-local-time")).ToHaveValueAsync("2026-01-01");
        await Assertions.Expect(_page.Locator("#reports-to-local-time-inclusive")).ToHaveValueAsync("2026-01-31");

        await _page.ClickAsync("button:has-text('Last 30 d')");
        await _page.WaitForURLAsync(url => url.Contains("days=30"));

        await _page.ReloadAsync();
        await _page.WaitForSelectorAsync(".stat-strip");
        await Assertions.Expect(_page.Locator("button:has-text('Last 30 d')")).ToHaveAttributeAsync("aria-pressed", "true");

        await _page.ClickAsync("button:text-is('Restore testing')");
        await _page.WaitForURLAsync(url => url.Contains("type=RestoreTesting") && url.Contains("days=30"));
    }

    [Test]
    public async Task SavedView_CanBeRenamed()
    {
        await LoginAsync("priya.nair@architonixlabs.com");
        var original = $"E2E view {Guid.NewGuid():N}"[..20];
        var renamed = $"Renamed {Guid.NewGuid():N}"[..18];

        await GotoAsync("/instances?env=Production");
        await _page.WaitForSelectorAsync(".dbt-table");
        await WaitForCircuitAsync();
        await _page.ClickAsync("button:has-text('Views')");
        await _page.ClickAsync("button:has-text('Save current view')");
        await _page.FillAsync("#new-view-name", original);
        await _page.ClickAsync("button:has-text('Save view')");
        await WaitForCircuitAsync();

        await _page.Locator($"[aria-label='Rename view {original}']").ClickAsync();
        var form = _page.Locator("[data-testid=saved-view-rename-form]");
        await form.Locator("input").FillAsync(renamed);
        await form.Locator("[data-testid=saved-view-rename-save]").ClickAsync();
        await WaitForCircuitAsync();

        await Assertions.Expect(_page.Locator($"a:has-text('{renamed}')")).ToBeVisibleAsync();
        await Assertions.Expect(_page.Locator($"a:has-text('{original}')")).ToHaveCountAsync(0);
    }

    [Test]
    public async Task NotificationBell_CanPauseAutoRefresh()
    {
        await LoginAsync("priya.nair@architonixlabs.com");
        await _page.WaitForSelectorAsync(".notif-bell-btn");
        await _page.ClickAsync(".notif-bell-btn");

        var pause = _page.Locator("[data-testid=notif-pause]");
        await pause.ClickAsync();
        await Assertions.Expect(pause).ToHaveAttributeAsync("aria-pressed", "true");
        await Assertions.Expect(_page.Locator("[data-testid=notif-updated]")).ToContainTextAsync("Auto-refresh paused");
        await Assertions.Expect(_page.Locator(".notif-bell-btn")).ToHaveAttributeAsync("aria-label", new System.Text.RegularExpressions.Regex("auto-refresh paused"));
    }

    [Test]
    public async Task TeamSubscription_AndQuietHours_CanBeSaved()
    {
        var teamName = $"E2E Team {Guid.NewGuid():N}"[..20];
        var options = new DbContextOptionsBuilder<DBTrackerDbContext>().UseSqlServer(TestConnectionString).Options;
        using (var db = new DBTrackerDbContext(options))
        {
            db.Teams.Add(new Team { Name = teamName });
            await db.SaveChangesAsync();
        }

        await LoginAsync("marcus.chen@architonixlabs.com");
        await GotoAsync("/notifications");
        await _page.WaitForSelectorAsync("[data-testid=quiet-hours]");
        await WaitForCircuitAsync();

        await _page.SelectOptionAsync("#notifications-scope", new SelectOptionValue { Label = "Owner team" });
        await WaitForCircuitAsync();
        await _page.SelectOptionAsync("[data-testid=subscription-team]", new SelectOptionValue { Label = teamName });
        await _page.SelectOptionAsync("#notifications-channel", new SelectOptionValue { Label = "Email only" });
        await _page.ClickAsync("[data-testid=subscription-add]");
        await WaitForCircuitAsync();
        await Assertions.Expect(_page.Locator($"text={teamName}").First).ToBeVisibleAsync();

        await _page.CheckAsync("[data-testid=quiet-hours-enabled]");
        await WaitForCircuitAsync();
        await _page.FillAsync("#quiet-start", "21:00");
        await _page.FillAsync("#quiet-end", "06:30");
        await _page.ClickAsync("[data-testid=quiet-hours-save]");
        await Assertions.Expect(_page.Locator("[data-testid=quiet-hours-status]")).ToContainTextAsync("Quiet hours 21:00 to 06:30");

        await _page.ReloadAsync();
        await _page.WaitForSelectorAsync("[data-testid=quiet-hours]");
        await Assertions.Expect(_page.Locator("#quiet-start")).ToHaveValueAsync("21:00");
    }

    [Test]
    public async Task SetupPage_RecordsTestEmailInDeliveryHistory()
    {
        await LoginAsync("admin@architonixlabs.com");
        await GotoAsync("/setup");
        await _page.WaitForSelectorAsync("#delivery-history");
        await WaitForCircuitAsync();

        await _page.FillAsync("#setup-recipient", "e2e-delivery@example.test");
        await _page.ClickAsync("button:has-text('Send test email')");
        await WaitForCircuitAsync();

        var row = _page.Locator("[data-testid=delivery-attempt-row]").First;
        await Assertions.Expect(row).ToContainTextAsync("Test email");
        await Assertions.Expect(row).ToContainTextAsync("admin@architonixlabs.com");
        // The Testing configuration has no email provider, so the attempt must read as disabled, never accepted.
        await Assertions.Expect(row).ToContainTextAsync("Disabled, not sent");
        await Assertions.Expect(row).Not.ToContainTextAsync("Accepted");
    }

    [Test]
    public async Task AssigningAnIncident_WritesAnAuditEvent()
    {
        await LoginAsync("priya.nair@architonixlabs.com");
        await GotoAsync("/instances?s=CONTOSO-HR-MYSQL");
        await _page.WaitForSelectorAsync(".dbt-table tbody tr");
        await WaitForCircuitAsync();
        await _page.ClickAsync(".dbt-table tbody tr");
        await _page.WaitForURLAsync(url => url.Contains("/instances/"));
        await WaitForCircuitAsync();

        var assignee = $"Noor {Guid.NewGuid():N}"[..12];
        var incidentCard = _page.Locator(".dbt-card", new PageLocatorOptions { HasText = "Open incident" });
        await incidentCard.Locator("input").First.FillAsync(assignee);
        await incidentCard.GetByRole(AriaRole.Button, new LocatorGetByRoleOptions { Name = "Assign" }).ClickAsync();
        await WaitForCircuitAsync();

        var options = new DbContextOptionsBuilder<DBTrackerDbContext>().UseSqlServer(TestConnectionString).Options;
        using var db = new DBTrackerDbContext(options);
        var evt = await db.AuditEvents.AsNoTracking()
            .Where(e => e.Action == "IncidentAssigned" && e.AfterJson!.Contains(assignee))
            .FirstOrDefaultAsync();
        Assert.That(evt, Is.Not.Null, "Assigning an incident must be audited.");
        Assert.That(evt!.ActorName, Is.EqualTo("priya.nair@architonixlabs.com"));
        Assert.That(evt.TargetType, Is.EqualTo("Incident"));
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
