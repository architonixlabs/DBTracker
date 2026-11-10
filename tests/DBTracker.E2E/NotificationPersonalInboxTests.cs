using DBTracker.Core.Entities;
using DBTracker.Core.Enums;
using DBTracker.Infrastructure;
using Microsoft.EntityFrameworkCore;
using Microsoft.Playwright;

namespace DBTracker.E2E;

/// <summary>E2E coverage for Epic 14's U07: per-user notification read state is genuinely independent between users, and a notification deep-links to its instance.</summary>
[TestFixture]
public class NotificationPersonalInboxTests
{
    private const string TestConnectionString =
        "Server=localhost;Database=DBTracker_Test;Integrated Security=True;TrustServerCertificate=True;";

    private IPage _page = null!;

    [SetUp]
    public void SetUp() { }

    [TearDown]
    public async Task TearDown()
    {
        if (_page is not null)
        {
            await _page.CloseAsync();
        }
    }

    [Test]
    public async Task TwoUsers_ReadingANotification_NeverAffectsTheOtherUsersInbox()
    {
        _page = await GlobalSetup.Server.Browser.NewPageAsync();

        var userA = await CreateFreshViewerAsync("e2e-inbox-a");
        var userB = await CreateFreshViewerAsync("e2e-inbox-b");

        // Both users' first-ever visit backfills everything that exists *so far* as read for them —
        // establishes a clean "now" baseline before the notification this test actually cares about exists.
        await LoginAsync(userA);
        await OpenBellAsync();
        await LoginAsync(userB);
        await OpenBellAsync();

        var marker = $"E2E-ALERT-{Guid.NewGuid():N}";
        var instanceId = await InsertFreshNotificationAsync(marker);

        // User A sees it unread, reads it, and is navigated to the instance.
        await LoginAsync(userA);
        await GotoAsync("/notifications");
        await _page.WaitForSelectorAsync(".notif-item");
        var itemForA = _page.Locator(".notif-item", new PageLocatorOptions { HasText = marker });
        await Assertions.Expect(itemForA).ToHaveClassAsync(new System.Text.RegularExpressions.Regex("unread"));

        await itemForA.ClickAsync();
        await _page.WaitForURLAsync(url => url.Contains($"/instances/{instanceId}"));

        // User B must still see the same notification as unread — A reading it changed nothing for B.
        await LoginAsync(userB);
        await GotoAsync("/notifications");
        await _page.WaitForSelectorAsync(".notif-item");
        var itemForB = _page.Locator(".notif-item", new PageLocatorOptions { HasText = marker });
        await Assertions.Expect(itemForB).ToHaveClassAsync(new System.Text.RegularExpressions.Regex("unread"));

        // And B can independently mark their own copy read without touching A's (already-read) copy.
        await _page.ClickAsync("button:has-text('Mark all read')");
        await _page.WaitForTimeoutAsync(500);
        await Assertions.Expect(_page.Locator(".notif-item", new PageLocatorOptions { HasText = marker })).Not.ToHaveClassAsync(new System.Text.RegularExpressions.Regex("unread"));
    }

    private async Task<(string Email, string Password)> CreateFreshViewerAsync(string prefix)
    {
        var email = $"{prefix}-{Guid.NewGuid():N}@architonixlabs.com";
        const string password = "Str0ngTempPass!";

        await LoginAsync(("admin@architonixlabs.com", "ArxDemo#2026!"));
        await GotoAsync("/users");
        await WaitForCircuitAsync();
        await FillByLabel("Display name", prefix);
        await FillByLabel("Email", email);
        await FillByLabel("Temporary password", password);
        await SelectByLabel("Role", "Viewer");
        await _page.ClickAsync("button[type=submit]:text('Create user')");
        await _page.WaitForTimeoutAsync(600);

        return (email, password);
    }

    private static async Task<int> InsertFreshNotificationAsync(string marker)
    {
        var options = new DbContextOptionsBuilder<DBTrackerDbContext>().UseSqlServer(TestConnectionString).Options;
        using var db = new DBTrackerDbContext(options);

        var instance = await db.DatabaseInstances.FirstAsync(i => i.Name == "ARXMAIL-PROD-01");
        db.Notifications.Add(new Notification
        {
            DatabaseInstanceId = instance.Id,
            InstanceName = instance.Name,
            Company = instance.Company,
            Message = $"{marker}: synthetic alert inserted directly for E2E (the alert sweep is disabled in Testing).",
            Severity = NotificationSeverity.Warning
        });
        await db.SaveChangesAsync();
        return instance.Id;
    }

    private Task LoginAsync((string Email, string Password) creds) => LoginAsync(creds.Email, creds.Password);

    private async Task LoginAsync(string email, string password = "ArxDemo#2026!")
    {
        await GotoAsync("/Account/Login");
        await _page.FillAsync("input[autocomplete=username]", email);
        await _page.FillAsync("input[autocomplete=current-password]", password);

        await _page.ClickAsync("button[type=submit]");
        await _page.WaitForURLAsync(url => !url.Contains("/Account/Login"), new PageWaitForURLOptions { Timeout = 15000 });

        await WaitForCircuitAsync();
    }

    private async Task OpenBellAsync()
    {
        await GotoAsync("/");
        await _page.WaitForSelectorAsync(".notif-bell-btn");
        await _page.ClickAsync(".notif-bell-btn");
        await _page.WaitForTimeoutAsync(400);
        await _page.ClickAsync(".notif-bell-btn"); // close it again
    }

    private Task GotoAsync(string path) =>
        _page.GotoAsync(TestServerFixture.BaseUrl + path, new PageGotoOptions { WaitUntil = WaitUntilState.NetworkIdle });

    private Task WaitForCircuitAsync() => _page.WaitForTimeoutAsync(800);

    private async Task FillByLabel(string label, string value)
    {
        await _page.Locator($"label:text-is('{label}') ~ input, label:text-is('{label}') + input").First.FillAsync(value);
    }

    private async Task SelectByLabel(string label, string optionText)
    {
        var select = _page.Locator($"label:text-is('{label}') ~ select, label:text-is('{label}') + select").First;
        await select.SelectOptionAsync(new SelectOptionValue { Label = optionText });
    }
}
