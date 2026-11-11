using System.Text;
using DBTracker.Core.Enums;
using DBTracker.Infrastructure;
using Microsoft.EntityFrameworkCore;
using Microsoft.Playwright;

namespace DBTracker.E2E;

/// <summary>G11, G12 and G23: history import, correction downloads, audit before/after, policy edits, conflict review,
/// snapshot provenance and the custom-range caption. Every test uses its own throwaway names.</summary>
[TestFixture]
public class EvidenceImportAndAuditTests
{
    private const string TestConnectionString = "Server=localhost;Database=DBTracker_Test;Integrated Security=True;TrustServerCertificate=True;";
    private IPage _page = null!;

    [TearDown]
    public async Task TearDown() => await _page.CloseAsync();

    private static DBTrackerDbContext Db() =>
        new(new DbContextOptionsBuilder<DBTrackerDbContext>().UseSqlServer(TestConnectionString).Options);

    private static string UniqueName(string prefix) => $"{prefix}-{Guid.NewGuid():N}"[..20].ToUpperInvariant();

    [Test]
    public async Task HistoryImport_AddsImportedEntries_ThenSkipsThemOnReimport_AndNeverCountsThemAsRestoreTests()
    {
        _page = await GlobalSetup.Server.Browser.NewPageAsync();
        await LoginAsync("priya.nair@architonixlabs.com");
        var name = UniqueName("E2E-HIST");
        await ImportInstancesAsync($"Name,Engine\n{name},SqlServer\n", "1 create");

        var history = "Kind,InstanceName,EventTime,TimeZone,Type,Status,SizeMb,DurationMinutes,Notes,PerformedBy,EvidenceReference\n" +
                      $"Backup,{name},2026-10-01 02:00,Europe/London,Full,Success,1024,12,From the old scheduler,,\n" +
                      $"Restore,{name},2026-10-15T09:30:00Z,,Test,Success,,35,Drill,Priya Nair,CHG-1042\n" +
                      $"Backup,{name},2026-10-02 02:00,,Full,Success,,,,,\n";
        await UploadHistoryAsync(history);

        var summary = _page.Locator("[data-testid=history-preview-summary]");
        await Assertions.Expect(summary).ToContainTextAsync("1 backup and 1 restore entries to add");
        await Assertions.Expect(summary).ToContainTextAsync("1 row(s) with errors");
        await Assertions.Expect(_page.Locator("text=has no time zone")).ToBeVisibleAsync();

        var download = await _page.RunAndWaitForDownloadAsync(() => _page.ClickAsync("[data-testid=history-download-errors]"));
        var fix = await File.ReadAllTextAsync(await download.PathAsync());
        Assert.That(fix, Does.StartWith("ImportError,SourceRow,Kind,InstanceName"));
        Assert.That(fix, Does.Contain($"Backup,{name},2026-10-02 02:00"), "The rows-to-fix file carries the row's own cells.");

        await _page.ClickAsync("[data-testid=history-apply]");
        await Assertions.Expect(_page.Locator("text=Imported 1 backup and 1 restore entries")).ToBeVisibleAsync();

        using (var db = Db())
        {
            var backup = await db.BackupRecords.AsNoTracking().SingleAsync(b => b.DatabaseInstance!.Name == name);
            Assert.That(backup.Source, Is.EqualTo(BackupSource.Imported));
            Assert.That(backup.RecordedByUserName, Is.EqualTo("priya.nair@architonixlabs.com"));
            Assert.That(backup.OccurredTimeZone, Is.EqualTo("Europe/London"));
            Assert.That(backup.TimestampUtc, Is.EqualTo(new DateTime(2026, 10, 1, 1, 0, 0)));
            var restore = await db.RestoreEvents.AsNoTracking().SingleAsync(r => r.DatabaseInstance!.Name == name);
            Assert.That(restore.Source, Is.EqualTo(BackupSource.Imported));
            Assert.That(restore.EvidenceReference, Is.EqualTo("CHG-1042"));
        }

        // The same file again adds nothing.
        await UploadHistoryAsync(history);
        await Assertions.Expect(summary).ToContainTextAsync("0 backup and 0 restore entries to add");
        await Assertions.Expect(summary).ToContainTextAsync("2 already recorded");
        await Assertions.Expect(_page.Locator("[data-testid=history-apply]")).ToBeDisabledAsync();

        // The instance shows the entries as imported, and restore-test coverage is still Never tested.
        var id = await InstanceIdAsync(name);
        await GotoAsync($"/instances/{id}");
        await WaitForCircuitAsync();
        await Assertions.Expect(_page.Locator("text=Imported by priya.nair@architonixlabs.com").First).ToBeVisibleAsync();
        await Assertions.Expect(_page.Locator("text=(imported, not counted)")).ToBeVisibleAsync();
        await Assertions.Expect(_page.Locator("text=Never tested").First).ToBeVisibleAsync();
    }

    [Test]
    public async Task InstanceImport_RowsToFix_CarryTheRowData_AndMatchByIdRenames()
    {
        _page = await GlobalSetup.Server.Browser.NewPageAsync();
        await LoginAsync("priya.nair@architonixlabs.com");
        var name = UniqueName("E2E-MATCH");
        var renamed = UniqueName("E2E-RENAMED");
        await ImportInstancesAsync($"Name,Engine\n{name},MySql\n", "1 create");
        var id = await InstanceIdAsync(name);

        await GotoAsync("/instances/import");
        await WaitForCircuitAsync();
        await _page.SelectOptionAsync("[data-testid=import-match-rule]", "Id");
        await _page.SetInputFilesAsync("input[type=file]", new FilePayload
        {
            Name = "by-id.csv",
            MimeType = "text/csv",
            Buffer = Encoding.UTF8.GetBytes($"Id,Name,Engine,Notes\n{id},{renamed},MySql,renamed by id\n,{UniqueName("E2E-BAD")},Oracle9,\"has, comma\"\n")
        });
        await _page.WaitForSelectorAsync("text=1 update");

        var download = await _page.RunAndWaitForDownloadAsync(() => _page.ClickAsync("[data-testid=import-download-errors]"));
        var fix = await File.ReadAllTextAsync(await download.PathAsync());
        Assert.That(fix, Does.StartWith("ImportError,SourceRow,Id,Name,Engine,Notes"));
        Assert.That(fix, Does.Contain("Oracle9,\"has, comma\""));

        await _page.ClickAsync("button:has-text('Apply')");
        await _page.WaitForURLAsync(url => url.Contains("/instances") && !url.Contains("/import"));
        using var db = Db();
        Assert.That((await db.DatabaseInstances.AsNoTracking().SingleAsync(i => i.Id == id)).Name, Is.EqualTo(renamed));
        var audit = await db.AuditEvents.AsNoTracking().Where(e => e.Action == "InstanceEdited" && e.TargetId == id.ToString()).OrderByDescending(e => e.Id).FirstAsync();
        Assert.That(audit.BeforeJson, Does.Contain(name));
        Assert.That(audit.AfterJson, Does.Contain(renamed));
    }

    [Test]
    public async Task InstanceEdit_IsAuditedWithBeforeAndAfter_ShownAndExportedOnTheAuditPage()
    {
        _page = await GlobalSetup.Server.Browser.NewPageAsync();
        await LoginAsync("priya.nair@architonixlabs.com");
        var name = UniqueName("E2E-AUDIT");
        await ImportInstancesAsync($"Name,Engine,Region\n{name},MySql,eu-west\n", "1 create");
        var id = await InstanceIdAsync(name);

        await GotoAsync($"/instances/{id}/edit");
        await WaitForCircuitAsync();
        await _page.FillAsync("#ed-region", "us-east");
        await _page.ClickAsync("button[type=submit]");
        await _page.WaitForURLAsync(url => url.EndsWith($"/instances/{id}"));

        await LoginAsync("angela.price@contoso.com");
        await GotoAsync("/audit");
        await WaitForCircuitAsync();
        await Assertions.Expect(_page.Locator("[data-testid=audit-retention]")).ToContainTextAsync("kept indefinitely");
        await _page.FillAsync("#audit-search", name);
        await _page.WaitForTimeoutAsync(800);
        var row = _page.Locator("tbody tr", new PageLocatorOptions { HasText = "InstanceEdited" }).First;
        await row.Locator("summary").ClickAsync();
        await Assertions.Expect(row).ToContainTextAsync("eu-west");
        await Assertions.Expect(row).ToContainTextAsync("us-east");

        var download = await _page.RunAndWaitForDownloadAsync(() => _page.ClickAsync("[data-testid=audit-export]"));
        var csv = await File.ReadAllTextAsync(await download.PathAsync());
        Assert.That(csv, Does.StartWith("TimestampUtc,ActorType,ActorName,Action,TargetType,TargetId,TargetName,Reason,BeforeJson,AfterJson"));
        Assert.That(csv, Does.Contain("us-east"));
    }

    [Test]
    public async Task ConcurrentEdit_ShowsTheOtherChange_AndMergesWhenFieldsDiffer()
    {
        _page = await GlobalSetup.Server.Browser.NewPageAsync();
        await LoginAsync("priya.nair@architonixlabs.com");
        var name = UniqueName("E2E-CONFLICT");
        await ImportInstancesAsync($"Name,Engine,Region,Notes\n{name},MySql,eu-west,original\n", "1 create");
        var id = await InstanceIdAsync(name);

        await GotoAsync($"/instances/{id}/edit");
        await WaitForCircuitAsync();

        // Someone else saves a different field meanwhile.
        using (var db = Db())
        {
            var stored = await db.DatabaseInstances.SingleAsync(i => i.Id == id);
            stored.Region = "us-east";
            await db.SaveChangesAsync();
        }

        await _page.FillAsync("#ed-notes", "edited in the form");
        await _page.ClickAsync("button[type=submit]");
        var conflict = _page.Locator("[data-testid=edit-conflict-table]");
        await Assertions.Expect(conflict).ToContainTextAsync("us-east");
        await Assertions.Expect(conflict).ToContainTextAsync("Only they changed");
        await Assertions.Expect(conflict).ToContainTextAsync("Only you changed");

        await _page.ClickAsync("[data-testid=edit-conflict-merge]");
        await _page.WaitForURLAsync(url => url.EndsWith($"/instances/{id}"));
        using var check = Db();
        var saved = await check.DatabaseInstances.AsNoTracking().SingleAsync(i => i.Id == id);
        Assert.That(saved.Region, Is.EqualTo("us-east"));
        Assert.That(saved.Notes, Is.EqualTo("edited in the form"));
    }

    [Test]
    public async Task PolicyEdit_PreviewsImpact_ThenSavesANewVersion()
    {
        _page = await GlobalSetup.Server.Browser.NewPageAsync();
        await LoginAsync("priya.nair@architonixlabs.com");
        var policy = UniqueName("E2E-POLICY");

        await GotoAsync("/ownership?tab=Policies");
        await WaitForCircuitAsync();
        await _page.FillAsync("#policy-name", policy);
        await _page.ClickAsync("button:has-text('Create policy')");
        var row = _page.Locator("tbody tr", new PageLocatorOptions { HasText = policy });
        await Assertions.Expect(row).ToContainTextAsync("v1");

        await row.Locator("[data-testid=policy-edit]").ClickAsync();
        await _page.FillAsync("[data-testid=policy-edit-interval]", "12");
        await _page.FillAsync("[data-testid=policy-edit-reason]", "Tighter cadence for production");
        await _page.ClickAsync("[data-testid=policy-edit-preview]");
        await Assertions.Expect(_page.Locator("[data-testid=policy-edit-impact]")).ToContainTextAsync("ExpectedBackupIntervalHours 24 to 12");
        await _page.ClickAsync("[data-testid=policy-edit-save]");
        await Assertions.Expect(row).ToContainTextAsync("v2");
        await Assertions.Expect(row).ToContainTextAsync("every 12 h");

        using var db = Db();
        var audit = await db.AuditEvents.AsNoTracking().Where(e => e.Action == "BackupPolicyUpdated" && e.TargetName == policy).SingleAsync();
        Assert.That(audit.Reason, Is.EqualTo("Tighter cadence for production"));
        Assert.That(audit.BeforeJson, Does.Contain("\"ExpectedBackupIntervalHours\":24"));
    }

    [Test]
    public async Task SavedSnapshot_RecordsARealPolicyVersionAndEvidenceProvenance()
    {
        _page = await GlobalSetup.Server.Browser.NewPageAsync();
        await LoginAsync("angela.price@contoso.com");
        await GotoAsync("/compliance");
        await WaitForCircuitAsync();
        await _page.ClickAsync("button:has-text('Save snapshot')");
        await _page.WaitForTimeoutAsync(800);

        using var db = Db();
        var snapshot = await db.ComplianceSnapshots.AsNoTracking().OrderByDescending(s => s.Id).FirstAsync();
        Assert.That(snapshot.PolicyVersion, Does.StartWith("rules-v"));
        Assert.That(snapshot.EvidenceProvenance, Does.Contain("restore-tested"));
        Assert.That(snapshot.RowsJson, Does.Contain("\"Evidence\""));

        await GotoAsync($"/compliance/snapshots/{snapshot.Id}");
        await WaitForCircuitAsync();
        await Assertions.Expect(_page.Locator("[data-testid=snapshot-provenance]")).ToContainTextAsync(snapshot.PolicyVersion);
    }

    [Test]
    public async Task BackupActivity_CustomRange_CaptionNamesTheDatesNotTheLastNDays()
    {
        _page = await GlobalSetup.Server.Browser.NewPageAsync();
        await LoginAsync("priya.nair@architonixlabs.com");
        await GotoAsync("/reports");
        await WaitForCircuitAsync();
        await Assertions.Expect(_page.Locator("[data-testid=backup-activity-caption]")).ToContainTextAsync("in the last 7 day(s)");

        await _page.ClickAsync("button:has-text('Custom')");
        await _page.FillAsync("#reports-from-local-time", "2026-10-01");
        await _page.FillAsync("#reports-to-local-time-inclusive", "2026-10-31");
        await _page.Locator("#reports-to-local-time-inclusive").DispatchEventAsync("change");
        var caption = _page.Locator("[data-testid=backup-activity-caption]");
        await Assertions.Expect(caption).ToContainTextAsync("from Oct 1, 2026 to Oct 31, 2026");
        await Assertions.Expect(caption).Not.ToContainTextAsync("in the last");
    }

    private async Task UploadHistoryAsync(string csv)
    {
        await GotoAsync("/instances/import-history");
        await _page.WaitForSelectorAsync("input[type=file]");
        await WaitForCircuitAsync();
        await _page.SetInputFilesAsync("input[type=file]", new FilePayload { Name = "history.csv", MimeType = "text/csv", Buffer = Encoding.UTF8.GetBytes(csv) });
        await _page.WaitForSelectorAsync("[data-testid=history-preview-summary]");
    }

    private async Task ImportInstancesAsync(string csv, string expectedSummary)
    {
        await GotoAsync("/instances/import");
        await _page.WaitForSelectorAsync("input[type=file]");
        await WaitForCircuitAsync();
        await _page.SetInputFilesAsync("input[type=file]", new FilePayload { Name = "import.csv", MimeType = "text/csv", Buffer = Encoding.UTF8.GetBytes(csv) });
        await _page.WaitForSelectorAsync($"text={expectedSummary}");
        await _page.ClickAsync("button:has-text('Apply')");
        await _page.WaitForURLAsync(url => url.Contains("/instances") && !url.Contains("/import"));
        await WaitForCircuitAsync();
    }

    private static async Task<int> InstanceIdAsync(string name)
    {
        using var db = Db();
        return await db.DatabaseInstances.AsNoTracking().Where(i => i.Name == name).Select(i => i.Id).SingleAsync();
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
