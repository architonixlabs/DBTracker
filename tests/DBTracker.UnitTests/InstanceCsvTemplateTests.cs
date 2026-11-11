using DBTracker.Core.Entities;
using DBTracker.Core.Enums;
using DBTracker.Core.Services;
using Xunit;

namespace DBTracker.UnitTests;

/// <summary>G23: the downloadable template documents every column and imports cleanly as downloaded.</summary>
public class InstanceCsvTemplateTests
{
    private static readonly Dictionary<string, DatabaseInstance> NoInstances = new(StringComparer.OrdinalIgnoreCase);
    private static readonly Dictionary<string, int> Teams = new(StringComparer.OrdinalIgnoreCase) { ["Platform Team"] = 4 };

    [Fact]
    public void TemplateAsDownloaded_ImportsNothing_AndReportsNoErrors()
    {
        var preview = InstanceCsvImporter.ParseForImport(InstanceCsvImporter.TemplateCsv(), NoInstances, teamIdsByName: Teams);

        Assert.Empty(preview.Rows);
        Assert.Empty(preview.Errors);
    }

    [Fact]
    public void Template_DescribesEveryColumn() =>
        Assert.Equal(InstanceCsvImporter.Columns, InstanceCsvImporter.ColumnGuide.Select(c => c.Name));

    [Fact]
    public void ExampleRow_WithItsHashRemoved_ImportsEveryNewColumn()
    {
        var lines = InstanceCsvImporter.TemplateCsv().Split('\n');
        var csv = lines[0] + "\n" + lines.Single(l => l.StartsWith("#EXAMPLE-")).TrimStart('#') + "\n";

        var preview = InstanceCsvImporter.ParseForImport(csv, NoInstances, teamIdsByName: Teams);

        Assert.Empty(preview.Errors);
        var row = Assert.Single(preview.Rows).Instance!;
        Assert.Equal("EXAMPLE-SQL-01", row.Name);
        Assert.Equal(@"\\fileserver\backups\arxmail", row.BackupFileLocation);
        Assert.Equal("eu-west", row.Region);
        Assert.Equal("sql-cluster-1", row.Cluster);
        Assert.Equal(InstanceCriticality.High, row.Criticality);
        Assert.Equal(4, row.OwnerTeamId);
        Assert.Equal("sam.okoro@example.com", row.BackupContactEmail);
        Assert.Equal("*.bak;*.trn", row.BackupFileNamePatterns);
        Assert.Equal(1433, row.ConnectivityPortOverride);
        Assert.Equal(90, row.RestoreTestIntervalDaysOverride);
    }

    [Fact]
    public void UnknownTeam_IsARowError_NotASilentBlank()
    {
        var preview = InstanceCsvImporter.ParseForImport("Name,Engine,OwnerTeam\nARX-02,MySql,Nobody\n", NoInstances, teamIdsByName: Teams);

        var error = Assert.Single(preview.Errors);
        Assert.Contains("Team 'Nobody' doesn't exist", error.Message);
    }

    [Theory]
    [InlineData("0")]
    [InlineData("70000")]
    public void PortOutOfRange_IsRejected(string port)
    {
        var preview = InstanceCsvImporter.ParseForImport($"Name,Engine,ConnectivityPort\nARX-03,MySql,{port}\n", NoInstances);

        Assert.Contains("between 1 and 65535", Assert.Single(preview.Errors).Message);
    }

    [Fact]
    public void ColumnLeftOut_KeepsTheStoredValue_EmptyCellClearsIt()
    {
        var stored = new DatabaseInstance { Id = 1, Name = "ARX-04", Engine = DatabaseEngine.MySql, Region = "eu-west", ConnectivityPortOverride = 3307 };
        var existing = new Dictionary<string, DatabaseInstance>(StringComparer.OrdinalIgnoreCase) { ["ARX-04"] = stored };

        var keep = Assert.Single(InstanceCsvImporter.ParseForImport("Name,Engine\nARX-04,MySql\n", existing).Rows).Instance!;
        Assert.Equal("eu-west", keep.Region);
        Assert.Equal(3307, keep.ConnectivityPortOverride);

        var clear = Assert.Single(InstanceCsvImporter.ParseForImport("Name,Engine,Region,ConnectivityPort\nARX-04,MySql,,\n", existing).Rows).Instance!;
        Assert.Equal("", clear.Region);
        Assert.Null(clear.ConnectivityPortOverride);
    }
}
