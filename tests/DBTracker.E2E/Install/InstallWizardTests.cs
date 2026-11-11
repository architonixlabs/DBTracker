using Microsoft.Playwright;
using static DBTracker.InstallE2E.InstallWizardDriver;

namespace DBTracker.InstallE2E;

/// <summary>The first-run wizard end to end, against a Production-mode app with no settings. Tests share one server and run in order.</summary>
[TestFixture]
[NonParallelizable]
public class InstallWizardTests
{
    private InstallServerFixture _server = null!;
    private IPage _page = null!;

    [OneTimeSetUp]
    public async Task StartServer()
    {
        _server = new InstallServerFixture();
        await _server.InitializeAsync();
    }

    [OneTimeTearDown]
    public async Task StopServer() => await _server.DisposeAsync();

    [SetUp]
    public async Task OpenPage() => _page = await _server.Browser.NewPageAsync();

    [TearDown]
    public async Task ClosePage() => await _page.CloseAsync();

    [Test, Order(1)]
    public async Task BeforeSetup_EveryPageGoesToTheWizard_AndAWrongKeyIsRejected()
    {
        await GotoAsync(_page, "/instances");
        Assert.That(_page.Url, Does.EndWith("/install"));

        await OpenAsync(_page);
        await _page.FillAsync("#install-key", "WRONG-KEYS-0000-0000");
        await _page.ClickAsync("button:has-text('Verify setup key')");

        await Assertions.Expect(_page.Locator(".alert-danger")).ToContainTextAsync("doesn't match");
        await Assertions.Expect(_page.Locator("h2.install-step-title")).ToHaveTextAsync("Enter the setup key");
    }

    [Test, Order(2)]
    public async Task WalkingTheWizard_CreatesTheDatabase_AndTheNewAdminCanSignIn()
    {
        await CompleteAsync(_page, _server, loadSampleData: false);

        Assert.That(File.Exists(Path.Combine(_server.DataDirectory, "dbtracker.config.json")), Is.True);
        Assert.That(File.Exists(Path.Combine(_server.DataDirectory, "setup-key.txt")), Is.False, "the setup key must be deleted once used");

        await LogInAsync(_page, AdminEmail, AdminPassword);
        await Assertions.Expect(_page.Locator("h1")).ToHaveTextAsync("Dashboard");

        // Without sample data the inventory starts empty and no demo account exists.
        await GotoAsync(_page, "/instances");
        await Assertions.Expect(_page.Locator("body")).Not.ToContainTextAsync("ARXMAIL-PROD-01");
    }

    [Test, Order(3)]
    public async Task AfterSetup_TheWizardReturns404()
    {
        var response = await GotoAsync(_page, "/install");
        Assert.That(response!.Status, Is.EqualTo(404));
        Assert.That(await _page.Locator("[data-testid=install-wizard]").CountAsync(), Is.Zero);
    }

    [Test, Order(4)]
    public async Task AfterARestart_SavedSettingsAreReadBack_AndTheAdminStillSignsIn()
    {
        await _server.RestartAsync();

        var response = await GotoAsync(_page, "/install");
        Assert.That(response!.Status, Is.EqualTo(404), "a restart must not reopen setup");

        await LogInAsync(_page, AdminEmail, AdminPassword);
        await Assertions.Expect(_page.Locator("h1")).ToHaveTextAsync("Dashboard");
    }
}

/// <summary>The sample-data option: example inventory plus the demo accounts, alongside the installer's own Admin.</summary>
[TestFixture]
[NonParallelizable]
public class InstallWithSampleDataTests
{
    private InstallServerFixture _server = null!;
    private IPage _page = null!;

    [OneTimeSetUp]
    public async Task StartServer()
    {
        _server = new InstallServerFixture();
        await _server.InitializeAsync();
        _page = await _server.Browser.NewPageAsync();
    }

    [OneTimeTearDown]
    public async Task StopServer()
    {
        await _page.CloseAsync();
        await _server.DisposeAsync();
    }

    [Test]
    public async Task SampleData_LoadsExampleInstances_AndTheDemoAccountsCanSignIn()
    {
        await CompleteAsync(_page, _server, loadSampleData: true);

        await LogInAsync(_page, "priya.nair@architonixlabs.com", "ArxDemo#2026!");
        await GotoAsync(_page, "/instances");
        await Assertions.Expect(_page.Locator("body")).ToContainTextAsync("ARXMAIL-PROD-01", new() { Timeout = 15000 });
    }
}
