using DBTracker.Core.Entities;
using DBTracker.Core.Enums;
using DBTracker.Core.Services;
using Xunit;

namespace DBTracker.UnitTests;

/// <summary>G24: re-importing must change only the columns in the file, and must not touch archived or duplicate-named instances.</summary>
public class InstanceCsvImporterSafetyTests
{
    private static DatabaseInstance Stored() => new()
    {
        Id = 7,
        Name = "ARX-01",
        Engine = DatabaseEngine.SqlServer,
        Company = "Old Co",
        OwnerTeamId = 3,
        Region = "eu-west",
        Cluster = "c1",
        ReplicaSet = "rs0",
        Criticality = InstanceCriticality.High,
        BackupContactEmail = "backup@architonixlabs.com",
        BackupFileNamePatterns = "*.bak",
        LastScanStatus = ScanStatus.Success,
        IsArchived = false,
        CreatedAtUtc = new DateTime(2025, 1, 1, 0, 0, 0, DateTimeKind.Utc),
    };

    [Fact]
    public void ApplyImportedFields_ChangesFileColumns_AndKeepsEverythingElse()
    {
        var stored = Stored();
        var preview = InstanceCsvImporter.ParseForImport("Name,Engine,Company\nARX-01,SqlServer,New Co\n",
            new Dictionary<string, DatabaseInstance>(StringComparer.OrdinalIgnoreCase) { ["ARX-01"] = Stored() });
        var row = Assert.Single(preview.Rows);
        Assert.Equal(ImportRowPlan.Update, row.Plan);

        InstanceCsvImporter.ApplyImportedFields(stored, row.Instance!);

        Assert.Equal("New Co", stored.Company);
        Assert.Equal(3, stored.OwnerTeamId);
        Assert.Equal("eu-west", stored.Region);
        Assert.Equal("c1", stored.Cluster);
        Assert.Equal("rs0", stored.ReplicaSet);
        Assert.Equal(InstanceCriticality.High, stored.Criticality);
        Assert.Equal("backup@architonixlabs.com", stored.BackupContactEmail);
        Assert.Equal("*.bak", stored.BackupFileNamePatterns);
        Assert.Equal(ScanStatus.Success, stored.LastScanStatus);
        Assert.Equal(new DateTime(2025, 1, 1, 0, 0, 0, DateTimeKind.Utc), stored.CreatedAtUtc);
    }

    [Fact]
    public void ApplyImportedFields_NeverUnarchives()
    {
        var stored = Stored();
        stored.IsArchived = true;

        InstanceCsvImporter.ApplyImportedFields(stored, new DatabaseInstance { Name = "ARX-01", Engine = DatabaseEngine.SqlServer });

        Assert.True(stored.IsArchived);
    }

    [Fact]
    public void BlockedName_IsAmbiguous_WithTheReasonShown()
    {
        var blocked = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase) { ["OLD-DB"] = "'OLD-DB' is archived." };

        var preview = InstanceCsvImporter.ParseForImport("Name,Engine\nold-db,MySql\nNEW-DB,MySql\n",
            new Dictionary<string, DatabaseInstance>(StringComparer.OrdinalIgnoreCase), blocked);

        var blockedRow = Assert.Single(preview.Rows, r => r.Plan == ImportRowPlan.Ambiguous);
        Assert.Equal("'OLD-DB' is archived.", blockedRow.AmbiguityReason);
        Assert.Equal(1, preview.CreateCount);
    }
}
