using Microsoft.EntityFrameworkCore;
using System.Text.Json;
using Microsoft.Playwright;

namespace DBTracker.E2E;

/// <summary>
/// G26: passkey sign-in, driven through Chromium's virtual authenticator (CDP WebAuthn domain). The authenticator belongs to the page,
/// so a passkey created on /account/passkeys is still there after signing out on the same page. Each test makes its own user.
/// </summary>
[TestFixture]
public class PasskeyTests
{
    private const string AdminEmail = "admin@architonixlabs.com";
    private const string DemoPassword = "ArxDemo#2026!";
    private const string TempPassword = "Str0ngTempPass!";
    private const string OwnPassword = "N3wOwnPassword!";

    private IPage _page = null!;
    private IPage? _admin;

    [TearDown]
    public async Task TearDown()
    {
        if (_admin is not null)
        {
            await _admin.CloseAsync();
            _admin = null;
        }
        await _page.CloseAsync();
    }

    [Test]
    public async Task UserAddsAPasskey_SignsOut_SignsBackInWithIt_RenamesAndRemovesIt()
    {
        var email = await CreateUserAsync("E2E Passkey User");
        _page = await GlobalSetup.Server.Browser.NewPageAsync();
        await AddVirtualAuthenticatorAsync(_page);
        await FirstSignInAsync(_page, email);

        // The account links under the user's name lead to the Passkeys page.
        await _page.ClickAsync("[data-testid=nav-passkeys]");
        await _page.WaitForURLAsync(url => url.Contains("/account/passkeys"));
        await Assertions.Expect(_page.Locator("[data-testid=passkey-empty]")).ToBeVisibleAsync();

        await AddPasskeyAsync(_page, "E2E laptop");
        var row = _page.Locator("[data-testid=passkey-row]");
        await Assertions.Expect(row).ToHaveCountAsync(1);
        await Assertions.Expect(row.Locator("[data-testid=passkey-name-cell]")).ToHaveTextAsync("E2E laptop");

        // Signed out, the login page offers the passkey and signs in without a password.
        await LogOutAsync(_page);
        await SignInWithPasskeyButtonAsync(_page);
        await _page.WaitForURLAsync(url => !url.Contains("/Account/Login"), new PageWaitForURLOptions { Timeout = 15000 });
        Assert.That(_page.Url, Does.Not.Contain("change-password"), "A user who already set their own password goes straight in.");

        // Rename, then remove.
        await GotoAsync(_page, "/account/passkeys");
        await WaitForPasskeyPageAsync(_page);
        await _page.ClickAsync("[data-testid=passkey-rename]");
        await _page.FillAsync("[data-testid=passkey-rename-input]", "E2E work laptop");
        await _page.ClickAsync("[data-testid=passkey-rename-save]");
        await Assertions.Expect(_page.Locator("[data-testid=passkey-name-cell]")).ToHaveTextAsync("E2E work laptop");
        await Assertions.Expect(_page.Locator("[data-testid=passkey-status]")).ToContainTextAsync("renamed");

        await _page.ClickAsync("[data-testid=passkey-remove]");
        await _page.ClickAsync("[data-testid=passkey-remove-confirm]");
        await Assertions.Expect(_page.Locator("[data-testid=passkey-empty]")).ToBeVisibleAsync();
        await Assertions.Expect(_page.Locator("[data-testid=passkey-status]")).ToContainTextAsync("removed");

        // The authenticator still holds the credential, but the server no longer accepts it.
        await LogOutAsync(_page);
        await SignInWithPasskeyButtonAsync(_page);
        await Assertions.Expect(_page.Locator("[data-testid=passkey-signin-error]")).ToContainTextAsync("isn't registered");
        Assert.That(_page.Url, Does.Contain("/Account/Login"));

        // Adding and removing are in the audit trail (the admin page from CreateUserAsync is still signed in).
        await GotoAsync(_admin!, "/audit");
        await _admin!.WaitForSelectorAsync("table");
        var audit = await _admin.ContentAsync();
        Assert.That(audit, Does.Contain("PasskeyAdded"));
        Assert.That(audit, Does.Contain("PasskeyRemoved"));
    }

    [Test]
    public async Task DeactivatedUser_CannotSignInWithTheirPasskey()
    {
        var email = await CreateUserAsync("E2E Passkey Deactivated");
        _page = await GlobalSetup.Server.Browser.NewPageAsync();
        await AddVirtualAuthenticatorAsync(_page);
        await FirstSignInAsync(_page, email);
        await GotoAsync(_page, "/account/passkeys");
        await WaitForPasskeyPageAsync(_page);
        await AddPasskeyAsync(_page, "");
        // A blank name falls back to the provider or a dated label, never an empty cell.
        await Assertions.Expect(_page.Locator("[data-testid=passkey-name-cell]")).Not.ToBeEmptyAsync();
        await LogOutAsync(_page);

        // Reload: the admin page has been idle since it created the user. The display name itself contains "Deactivated",
        // so check the status badge and the stored flag, not the row text.
        await GotoAsync(_admin!, "/users");
        await _admin!.WaitForSelectorAsync("button[type=submit]");
        await WaitForCircuitAsync(_admin);
        var userRow = _admin.Locator("tr", new PageLocatorOptions { HasText = email });
        await userRow.Locator("button:has-text('Deactivate')").ClickAsync();
        await Assertions.Expect(userRow.Locator(".badge-health", new LocatorLocatorOptions { HasText = "Deactivated" })).ToBeVisibleAsync();
        using (var db = new DBTracker.Infrastructure.DBTrackerDbContext(new DbContextOptionsBuilder<DBTracker.Infrastructure.DBTrackerDbContext>()
                   .UseSqlServer("Server=localhost;Database=DBTracker_Test;Integrated Security=True;TrustServerCertificate=True;").Options))
        {
            Assert.That(await db.Users.Where(u => u.Email == email).Select(u => u.IsActive).SingleAsync(), Is.False, "The account must really be deactivated.");
        }

        await SignInWithPasskeyButtonAsync(_page);
        await Assertions.Expect(_page.Locator("[data-testid=passkey-signin-error]")).ToContainTextAsync("deactivated");
        Assert.That(_page.Url, Does.Contain("/Account/Login"));
    }

    /// <summary>Admin creates a Viewer with a temporary password. Leaves the admin page open on /users.</summary>
    private async Task<string> CreateUserAsync(string displayName)
    {
        _admin = await GlobalSetup.Server.Browser.NewPageAsync();
        await PasswordSignInAsync(_admin, AdminEmail, DemoPassword);
        await GotoAsync(_admin, "/users");
        await _admin.WaitForSelectorAsync("button[type=submit]");
        await WaitForCircuitAsync(_admin);
        var email = $"e2e-passkey-{Guid.NewGuid():N}@architonixlabs.com";
        await FillByLabel(_admin, "Display name", displayName);
        await FillByLabel(_admin, "Email", email);
        await FillByLabel(_admin, "Temporary password", TempPassword);
        await _admin.Locator("label:text-is('Role') ~ select, label:text-is('Role') + select").First
            .SelectOptionAsync(new SelectOptionValue { Label = "Viewer" });
        await _admin.ClickAsync("button[type=submit]:text('Create user')");
        await Assertions.Expect(_admin.Locator("tr", new PageLocatorOptions { HasText = email })).ToBeVisibleAsync();
        return email;
    }

    /// <summary>Password sign-in with the temporary password, then the forced change, as a new user does first.</summary>
    private static async Task FirstSignInAsync(IPage page, string email)
    {
        await PasswordSignInAsync(page, email, TempPassword);
        await page.WaitForURLAsync(url => url.Contains("/account/change-password"));
        await WaitForCircuitAsync(page);
        await FillByLabel(page, "Current password", TempPassword);
        await FillByLabel(page, "New password", OwnPassword);
        await page.ClickAsync("button[type=submit]:text('Change password')");
        await Assertions.Expect(page.Locator(".dbt-toast")).ToContainTextAsync("Password changed");
    }

    private static async Task AddVirtualAuthenticatorAsync(IPage page)
    {
        // Chrome's virtual authenticator answers a conditional (autofill) request by itself, which a real browser only does
        // after the user picks a passkey. Report no conditional support so the page skips autofill and these tests drive the
        // explicit "Sign in with a passkey" path deterministically. Defined non-writable so the polyfill can't replace it.
        await page.AddInitScriptAsync("""
            if (window.PublicKeyCredential) {
              const real = PublicKeyCredential.getClientCapabilities?.bind(PublicKeyCredential);
              Object.defineProperty(PublicKeyCredential, 'getClientCapabilities', {
                configurable: false, writable: false,
                value: async () => ({ ...(real ? await real() : { passkeyPlatformAuthenticator: true, userVerifyingPlatformAuthenticator: true }), conditionalGet: false })
              });
            }
            """);
        var cdp = await page.Context.NewCDPSessionAsync(page);
        await cdp.SendAsync("WebAuthn.enable", new Dictionary<string, object> { ["enableUI"] = false });
        JsonElement? added = await cdp.SendAsync("WebAuthn.addVirtualAuthenticator", new Dictionary<string, object>
        {
            ["options"] = new Dictionary<string, object>
            {
                ["protocol"] = "ctap2",
                ["transport"] = "internal",
                ["hasResidentKey"] = true,
                ["hasUserVerification"] = true,
                ["isUserVerified"] = true,
                ["automaticPresenceSimulation"] = true,
            },
        });
        Assert.That(added?.GetProperty("authenticatorId").GetString(), Is.Not.Empty);
    }

    private static async Task WaitForPasskeyPageAsync(IPage page)
    {
        await page.WaitForSelectorAsync("[data-testid=passkey-add-panel]");
        await WaitForCircuitAsync(page);
    }

    private static async Task AddPasskeyAsync(IPage page, string name)
    {
        await WaitForPasskeyPageAsync(page);
        var add = page.Locator("[data-testid=passkey-add]");
        await Assertions.Expect(add).ToBeVisibleAsync(new LocatorAssertionsToBeVisibleOptions { Timeout = 10000 });
        await page.FillAsync("[data-testid=passkey-name]", name);
        await add.ClickAsync();
        await Assertions.Expect(page.Locator("[data-testid=passkey-status]")).ToContainTextAsync("Passkey added", new LocatorAssertionsToContainTextOptions { Timeout = 15000 });
        await Assertions.Expect(page.Locator("[data-testid=passkey-error]")).ToBeEmptyAsync();
    }

    private static async Task SignInWithPasskeyButtonAsync(IPage page)
    {
        if (!page.Url.Contains("/Account/Login"))
        {
            await GotoAsync(page, "/Account/Login");
        }
        var button = page.Locator("[data-testid=passkey-signin]");
        await Assertions.Expect(button).ToBeVisibleAsync();
        await button.ClickAsync();
    }

    private static async Task LogOutAsync(IPage page)
    {
        await page.ClickAsync("form[action*='Logout'] button[type=submit]");
        await page.WaitForURLAsync(url => url.Contains("/Account/Login"));
    }

    private static async Task PasswordSignInAsync(IPage page, string email, string password)
    {
        await GotoAsync(page, "/Account/Login");
        await page.FillAsync("input[autocomplete~=username]", email);
        await page.FillAsync("input[autocomplete=current-password]", password);
        await page.ClickAsync("button[type=submit]");
        await page.WaitForURLAsync(url => !url.Contains("/Account/Login"), new PageWaitForURLOptions { Timeout = 15000 });
        await WaitForCircuitAsync(page);
    }

    private static Task GotoAsync(IPage page, string path) =>
        page.GotoAsync(TestServerFixture.BaseUrl + path, new PageGotoOptions { WaitUntil = WaitUntilState.Load });

    private static Task WaitForCircuitAsync(IPage page) => page.WaitForTimeoutAsync(800);

    private static Task FillByLabel(IPage page, string label, string value) =>
        page.Locator($"label:text-is('{label}') ~ input, label:text-is('{label}') + input").First.FillAsync(value);
}
