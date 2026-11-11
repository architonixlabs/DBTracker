using Microsoft.EntityFrameworkCore;
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

    /// <summary>G23/G24: the new columns import, a re-import changes only the file's columns (nothing else is reset), and a
    /// name belonging to an archived instance is blocked instead of silently unarchiving it.</summary>
    [Test]
    public async Task Reimport_ChangesOnlyFileColumns_AndNeverUnarchives()
    {
        _page = await GlobalSetup.Server.Browser.NewPageAsync();
        await LoginAsync("priya.nair@architonixlabs.com");
        var name = $"E2E-SAFE-{Guid.NewGuid():N}".Substring(0, 20).ToUpperInvariant();

        await ImportCsvAsync($"Name,Engine,Region,Criticality,ConnectivityPort\n{name},SqlServer,eu-west,High,14330\n", "1 create");

        var options = new Microsoft.EntityFrameworkCore.DbContextOptionsBuilder<DBTracker.Infrastructure.DBTrackerDbContext>()
            .UseSqlServer("Server=localhost;Database=DBTracker_Test;Integrated Security=True;TrustServerCertificate=True;").Options;
        var created = new DateTime(2025, 1, 1, 0, 0, 0, DateTimeKind.Utc);
        using (var db = new DBTracker.Infrastructure.DBTrackerDbContext(options))
        {
            await db.DatabaseInstances.Where(i => i.Name == name).ExecuteUpdateAsync(u => u
                .SetProperty(i => i.CreatedAtUtc, created)
                .SetProperty(i => i.LastScanStatus, DBTracker.Core.Enums.ScanStatus.Success));
        }

        // A file with only Application: everything else stays as stored.
        await ImportCsvAsync($"Name,Engine,Application\n{name},SqlServer,Revised App\n", "1 update");
        using (var db = new DBTracker.Infrastructure.DBTrackerDbContext(options))
        {
            var stored = await db.DatabaseInstances.AsNoTracking().SingleAsync(i => i.Name == name);
            Assert.That(stored.Application, Is.EqualTo("Revised App"));
            Assert.That(stored.Region, Is.EqualTo("eu-west"));
            Assert.That(stored.Criticality, Is.EqualTo(DBTracker.Core.Enums.InstanceCriticality.High));
            Assert.That(stored.ConnectivityPortOverride, Is.EqualTo(14330));
            Assert.That(stored.LastScanStatus, Is.EqualTo(DBTracker.Core.Enums.ScanStatus.Success));
            Assert.That(stored.CreatedAtUtc, Is.EqualTo(created));

            await db.DatabaseInstances.Where(i => i.Name == name).ExecuteUpdateAsync(u => u.SetProperty(i => i.IsArchived, true));
        }

        // Archived: the row is blocked with the reason, and nothing can be applied.
        await GotoAsync("/instances/import");
        await _page.WaitForSelectorAsync("input[type=file]");
        await WaitForCircuitAsync();
        await _page.SetInputFilesAsync("input[type=file]", new FilePayload
        {
            Name = "archived.csv",
            MimeType = "text/csv",
            Buffer = Encoding.UTF8.GetBytes($"Name,Engine,Application\n{name},SqlServer,Should not apply\n")
        });
        await Assertions.Expect(_page.Locator("tbody tr", new PageLocatorOptions { HasText = name })).ToContainTextAsync("is archived");
        await Assertions.Expect(_page.Locator("button:has-text('Apply')")).ToBeDisabledAsync();

        using (var db = new DBTracker.Infrastructure.DBTrackerDbContext(options))
        {
            Assert.That((await db.DatabaseInstances.AsNoTracking().SingleAsync(i => i.Name == name)).IsArchived, Is.True);
        }
    }

    [Test]
    public async Task DownloadedTemplate_HasTheExampleRowAndAllowedValues()
    {
        _page = await GlobalSetup.Server.Browser.NewPageAsync();
        await LoginAsync("priya.nair@architonixlabs.com");
        await GotoAsync("/instances/import");
        await WaitForCircuitAsync();

        await _page.ClickAsync("summary:has-text('Column guide')");
        await Assertions.Expect(_page.Locator("table[aria-label='Import columns and the values they accept']")).ToContainTextAsync("RestoreTestIntervalDays");

        var download = await _page.RunAndWaitForDownloadAsync(() => _page.ClickAsync("button:has-text('Download CSV template')"));
        var path = await download.PathAsync();
        var text = await File.ReadAllTextAsync(path);
        Assert.That(text, Does.StartWith("Name,Engine,"));
        Assert.That(text, Does.Contain("#EXAMPLE-SQL-01,SqlServer"));
        Assert.That(text, Does.Contain("# Engine (required): SqlServer | MySql"));
    }

    private async Task ImportCsvAsync(string csv, string expectedSummary)
    {
        await GotoAsync("/instances/import");
        await _page.WaitForSelectorAsync("input[type=file]");
        await WaitForCircuitAsync();
        await _page.SetInputFilesAsync("input[type=file]", new FilePayload
        {
            Name = "import.csv",
            MimeType = "text/csv",
            Buffer = Encoding.UTF8.GetBytes(csv)
        });
        await _page.WaitForSelectorAsync($"text={expectedSummary}");
        await _page.ClickAsync("button:has-text('Apply')");
        await _page.WaitForURLAsync(url => url.Contains("/instances") && !url.Contains("/import"));
        await WaitForCircuitAsync();
    }

    /// <summary>G04: an invalid value blocks the whole batch with the instance named, and nothing is saved or audited.
    /// A valid batch is audited under one batch id.</summary>
    [Test]
    public async Task BulkEdit_InvalidValue_SavesNothing_ValidBatchIsAuditedWithABatchId()
    {
        _page = await GlobalSetup.Server.Browser.NewPageAsync();
        await LoginAsync("priya.nair@architonixlabs.com");

        var ids = await SelectedIdsAsync("EDIQRA-INVENTORY-PG");
        await GotoAsync($"/instances/bulk-edit?ids={ids}");
        await WaitForCircuitAsync();

        var badEmail = $"not-an-email-{Guid.NewGuid():N}"[..24];
        await _page.CheckAsync("#set-owner");
        await _page.FillAsync("#bulk-company", "Ediqra");
        await _page.FillAsync("#bulk-contact-email", badEmail);
        await _page.ClickAsync("button:has-text('Preview changes')");
        await _page.WaitForTimeoutAsync(500);
        await _page.ClickAsync("button:has-text('Apply to')");
        await _page.WaitForTimeoutAsync(600);

        var problems = _page.Locator("[data-testid=bulk-edit-problems]");
        await Assertions.Expect(problems).ToContainTextAsync("Nothing was saved");
        await Assertions.Expect(problems).ToContainTextAsync("EDIQRA-INVENTORY-PG: Contact email is not a valid email address.");
        Assert.That(_page.Url, Does.Contain("/bulk-edit"), "A refused batch must stay on the page.");

        // Fix the value, preview again, apply: audited with a batch id; the bad value was never saved or audited.
        var tagValue = $"e2e-batch-{Guid.NewGuid():N}"[..16];
        await _page.UncheckAsync("#set-owner");
        await _page.CheckAsync("#set-tags");
        await _page.FillAsync("#bulk-tags", tagValue);
        await _page.ClickAsync("button:has-text('Preview changes')");
        await _page.WaitForTimeoutAsync(500);
        await _page.ClickAsync("button:has-text('Apply to')");
        await _page.WaitForURLAsync(url => !url.Contains("/bulk-edit"));

        // Audit is Admin/Auditor only; Angela reads it.
        await LoginAsync("angela.price@contoso.com");
        await GotoAsync("/audit");
        await _page.WaitForSelectorAsync("tbody tr");
        var auditBody = await _page.Locator("body").InnerTextAsync();
        Assert.That(auditBody, Does.Match(@"Bulk edit batch [0-9a-f]{8}"));
        Assert.That(auditBody, Does.Not.Contain(badEmail));
    }

    private async Task<string> SelectedIdsAsync(string instanceName)
    {
        await GotoAsync($"/instances?s={instanceName}");
        await _page.WaitForSelectorAsync(".dbt-table tbody tr");
        await WaitForCircuitAsync();
        await _page.ClickAsync(".dbt-table tbody tr");
        await _page.WaitForURLAsync(url => System.Text.RegularExpressions.Regex.IsMatch(url, @"/instances/\d+"));
        return System.Text.RegularExpressions.Regex.Match(_page.Url, @"/instances/(\d+)").Groups[1].Value;
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
