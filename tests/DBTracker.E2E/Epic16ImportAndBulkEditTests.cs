using System.Text;
using Microsoft.Playwright;

namespace DBTracker.E2E;

/// <summary>E2E coverage for Epic 16's U17: CSV re-import matching/updating instead of duplicating, and the bulk-edit before/after diff preview.</summary>
[TestFixture]
public class Epic16ImportAndBulkEditTests
{
    private IPage _page = null!;

    [TearDown]
    public async Task TearDown() => await _page.CloseAsync();

    [Test]
    public async Task ReimportingACsv_UpdatesTheMatchingExistingInstance_AndCreatesOnlyTheNewOne_WithoutDuplicating()
    {
        // Entirely self-contained (two throwaway names, never touching seeded fixture data) so
        // this test can't leak a mutation into any other test sharing the same database.
        _page = await GlobalSetup.Server.Browser.NewPageAsync();
        await LoginAsync("priya.nair@architonixlabs.com");

        var reimportedName = $"E2E-REIMPORT-{Guid.NewGuid():N}".Substring(0, 20).ToUpperInvariant();
        var newName = $"E2E-IMPORT-{Guid.NewGuid():N}".Substring(0, 20).ToUpperInvariant();

        // First import: creates the instance that will be matched and updated by the second import.
        await GotoAsync("/instances/import");
        await _page.WaitForSelectorAsync("input[type=file]");
        await WaitForCircuitAsync();
        await _page.SetInputFilesAsync("input[type=file]", new FilePayload
        {
            Name = "first-import.csv",
            MimeType = "text/csv",
            Buffer = Encoding.UTF8.GetBytes($"Name,Engine,Application\n{reimportedName},PostgreSql,Original App\n")
        });
        await _page.WaitForSelectorAsync("text=1 create");
        await _page.ClickAsync("button:has-text('Apply')");
        await _page.WaitForURLAsync(url => url.Contains("/instances") && !url.Contains("/import"));
        await WaitForCircuitAsync();

        // Second import: re-imports the same name with a changed Application, plus a brand-new name.
        await GotoAsync("/instances/import");
        await _page.WaitForSelectorAsync("input[type=file]");
        await WaitForCircuitAsync();
        var csv = "Name,Engine,Application\n" +
                  $"{reimportedName},PostgreSql,Revised App via E2E Reimport\n" +
                  $"{newName},MySql,E2E Test App\n";
        await _page.SetInputFilesAsync("input[type=file]", new FilePayload
        {
            Name = "reimport.csv",
            MimeType = "text/csv",
            Buffer = Encoding.UTF8.GetBytes(csv)
        });
        await _page.WaitForTimeoutAsync(700);

        // Preview classifies the matched row as an update and the new name as a create.
        await Assertions.Expect(_page.Locator("tbody tr", new PageLocatorOptions { HasText = reimportedName })).ToContainTextAsync("Update");
        await Assertions.Expect(_page.Locator("tbody tr", new PageLocatorOptions { HasText = newName })).ToContainTextAsync("Create");

        await _page.ClickAsync("button:has-text('Apply')");
        await _page.WaitForURLAsync(url => url.Contains("/instances") && !url.Contains("/import"));
        await WaitForCircuitAsync();

        // Re-importing never duplicates: exactly one row for the existing name, now showing the
        // updated Application value, and exactly one row for the newly created name.
        await GotoAsync($"/instances?s={reimportedName}");
        await _page.WaitForTimeoutAsync(600);
        await Assertions.Expect(_page.Locator(".dbt-table tbody tr")).ToHaveCountAsync(1);
        await Assertions.Expect(_page.Locator(".dbt-table tbody tr")).ToContainTextAsync("Revised App via E2E Reimport");

        await GotoAsync($"/instances?s={newName}");
        await _page.WaitForTimeoutAsync(600);
        await Assertions.Expect(_page.Locator(".dbt-table tbody tr")).ToHaveCountAsync(1);
    }

    [Test]
    public async Task BulkEdit_ShowsBeforeAfterDiffPreview_AndAppliesOnlyToSelectedInstances()
    {
        _page = await GlobalSetup.Server.Browser.NewPageAsync();
        await LoginAsync("priya.nair@architonixlabs.com");

        await GotoAsync("/instances?s=EDIQRA-INVENTORY-PG");
        await _page.WaitForSelectorAsync(".dbt-table tbody tr");
        await WaitForCircuitAsync();

        var row = _page.Locator(".dbt-table tbody tr", new PageLocatorOptions { HasText = "EDIQRA-INVENTORY-PG" });
        await row.Locator("input[type=checkbox]").CheckAsync();
        await _page.WaitForTimeoutAsync(400);
        await _page.ClickAsync("button:has-text('Bulk edit selected')");
        await _page.WaitForURLAsync(url => url.Contains("/instances/bulk-edit"));
        await WaitForCircuitAsync();

        await _page.CheckAsync("#set-tags");
        var tagValue = $"e2e-bulk-{Guid.NewGuid():N}".Substring(0, 16);
        await _page.FillAsync("input[placeholder*='comma, separated']", tagValue);
        await _page.ClickAsync("button:has-text('Preview changes')");
        await _page.WaitForTimeoutAsync(500);

        await Assertions.Expect(_page.Locator("text=Before / after preview")).ToBeVisibleAsync();
        await Assertions.Expect(_page.Locator("table tbody tr", new PageLocatorOptions { HasText = "Tags" })).ToContainTextAsync(tagValue);

        await _page.ClickAsync("button:has-text('Apply to')");
        await _page.WaitForURLAsync(url => url.Contains("/instances") && !url.Contains("/bulk-edit"));
        await WaitForCircuitAsync();

        await GotoAsync("/instances?s=EDIQRA-INVENTORY-PG");
        await _page.WaitForSelectorAsync(".dbt-table tbody tr");
        await _page.ClickAsync(".dbt-table tbody tr");
        await _page.WaitForURLAsync(url => url.Contains("/instances/") && !url.Contains("bulk-edit"));
        await WaitForCircuitAsync();

        await Assertions.Expect(_page.Locator("text=" + tagValue)).ToBeVisibleAsync();
    }

    [Test]
    public async Task BulkEdit_CannotArchive_NoArchiveControlExistsOnThePage()
    {
        _page = await GlobalSetup.Server.Browser.NewPageAsync();
        await LoginAsync("priya.nair@architonixlabs.com");

        await GotoAsync("/instances/bulk-edit?ids=1");
        await _page.WaitForTimeoutAsync(500);

        await Assertions.Expect(_page.Locator("text=Archive")).Not.ToBeVisibleAsync();
        await Assertions.Expect(_page.Locator("text=separate, individually-confirmed action")).ToBeVisibleAsync();
    }

    private Task LoginAsync(string email) => LoginAsync(email, "ArxDemo#2026!");

    private async Task LoginAsync(string email, string password)
    {
        await GotoAsync("/Account/Login");
        await _page.FillAsync("input[autocomplete=username]", email);
        await _page.FillAsync("input[autocomplete=current-password]", password);
        await _page.ClickAsync("button[type=submit]");
        await _page.WaitForURLAsync(url => !url.Contains("/Account/Login"), new PageWaitForURLOptions { Timeout = 15000 });
        await WaitForCircuitAsync();
    }

    private Task GotoAsync(string path) =>
        _page.GotoAsync(TestServerFixture.BaseUrl + path, new PageGotoOptions { WaitUntil = WaitUntilState.Load });

    private Task WaitForCircuitAsync() => _page.WaitForTimeoutAsync(800);
}
