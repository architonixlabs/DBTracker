using Microsoft.Playwright;

namespace DBTracker.E2E;

/// <summary>
/// End-to-end proof of the QR "sign in with your phone" flow: two separate browser contexts stand
/// in for the desktop (unauthenticated) and the phone (already signed in), exactly as they'd be two
/// different devices in reality.
/// </summary>
[TestFixture]
public class DeviceLinkLoginTests
{
    [Test]
    public async Task ScanningAndApprovingOnPhone_SignsInDesktop()
    {
        var browser = GlobalSetup.Server.Browser;

        await using var desktopContext = await browser.NewContextAsync();
        await using var phoneContext = await browser.NewContextAsync();

        var desktop = await desktopContext.NewPageAsync();
        var phone = await phoneContext.NewPageAsync();

        // Desktop: open the login page and the QR panel.
        await desktop.GotoAsync(TestServerFixture.BaseUrl + "/Account/Login", new PageGotoOptions { WaitUntil = WaitUntilState.Load });
        await desktop.ClickAsync("#qr-toggle");
        await desktop.WaitForSelectorAsync("#qr-image[src]");
        await desktop.WaitForFunctionAsync("() => document.getElementById('qr-image').src.includes('/device-link/qr/')");

        var qrSrc = await desktop.GetAttributeAsync("#qr-image", "src");
        Assert.That(qrSrc, Is.Not.Null.And.Not.Empty);
        var token = ExtractToken(qrSrc!);

        // Phone: already signed in (simulating a trusted device), opens the scanned link and approves.
        await phone.GotoAsync(TestServerFixture.BaseUrl + "/Account/Login", new PageGotoOptions { WaitUntil = WaitUntilState.Load });
        await phone.FillAsync("input[autocomplete=username]", "priya.nair@architonixlabs.com");
        await phone.FillAsync("input[autocomplete=current-password]", "ArxDemo#2026!");
        await phone.ClickAsync("button[type=submit]");
        await phone.WaitForURLAsync(url => !url.Contains("/Account/Login"), new PageWaitForURLOptions { Timeout = 15000 });

        await phone.GotoAsync(TestServerFixture.BaseUrl + $"/Account/LinkDevice?token={token}", new PageGotoOptions { WaitUntil = WaitUntilState.Load });
        await Assertions.Expect(phone.Locator("text=priya.nair@architonixlabs.com")).ToBeVisibleAsync();

        await phone.ClickAsync("button:has-text('Approve')");
        await phone.WaitForURLAsync(url => url.Contains("/Account/LinkDeviceDone"), new PageWaitForURLOptions { Timeout = 15000 });
        await Assertions.Expect(phone.Locator("text=Request approved")).ToBeVisibleAsync();

        // Desktop: polling should pick up the approval and sign itself in, landing on the dashboard.
        await desktop.WaitForURLAsync(url => url == TestServerFixture.BaseUrl + "/" || url == TestServerFixture.BaseUrl, new PageWaitForURLOptions { Timeout = 15000 });
        await desktop.WaitForSelectorAsync(".notif-bell-btn", new PageWaitForSelectorOptions { Timeout = 15000 });
        await Assertions.Expect(desktop.Locator("text=priya.nair@architonixlabs.com")).ToBeVisibleAsync();

        await desktop.CloseAsync();
        await phone.CloseAsync();
    }

    [Test]
    public async Task DenyingOnPhone_LeavesDesktopSignedOut()
    {
        var browser = GlobalSetup.Server.Browser;

        await using var desktopContext = await browser.NewContextAsync();
        await using var phoneContext = await browser.NewContextAsync();

        var desktop = await desktopContext.NewPageAsync();
        var phone = await phoneContext.NewPageAsync();

        await desktop.GotoAsync(TestServerFixture.BaseUrl + "/Account/Login", new PageGotoOptions { WaitUntil = WaitUntilState.Load });
        await desktop.ClickAsync("#qr-toggle");
        await desktop.WaitForFunctionAsync("() => document.getElementById('qr-image').src.includes('/device-link/qr/')");
        var token = ExtractToken((await desktop.GetAttributeAsync("#qr-image", "src"))!);

        await phone.GotoAsync(TestServerFixture.BaseUrl + "/Account/Login", new PageGotoOptions { WaitUntil = WaitUntilState.Load });
        await phone.FillAsync("input[autocomplete=username]", "marcus.chen@architonixlabs.com");
        await phone.FillAsync("input[autocomplete=current-password]", "ArxDemo#2026!");
        await phone.ClickAsync("button[type=submit]");
        await phone.WaitForURLAsync(url => !url.Contains("/Account/Login"), new PageWaitForURLOptions { Timeout = 15000 });

        await phone.GotoAsync(TestServerFixture.BaseUrl + $"/Account/LinkDevice?token={token}", new PageGotoOptions { WaitUntil = WaitUntilState.Load });
        await phone.ClickAsync("button:has-text('Deny')");
        await phone.WaitForURLAsync(url => url.Contains("/Account/LinkDeviceDone"), new PageWaitForURLOptions { Timeout = 15000 });
        await Assertions.Expect(phone.Locator("text=Request denied")).ToBeVisibleAsync();

        await desktop.WaitForSelectorAsync("#qr-status:has-text('denied')", new PageWaitForSelectorOptions { Timeout = 15000 });
        // Desktop never navigated away from the login page.
        Assert.That(desktop.Url, Does.Contain("/Account/Login"));

        await desktop.CloseAsync();
        await phone.CloseAsync();
    }

    private static string ExtractToken(string qrImageSrc)
    {
        var marker = "/device-link/qr/";
        var start = qrImageSrc.IndexOf(marker, StringComparison.Ordinal) + marker.Length;
        var end = qrImageSrc.IndexOf('?', start);
        return end > start ? qrImageSrc[start..end] : qrImageSrc[start..];
    }
}
