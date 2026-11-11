using DBTracker.Core.Entities;
using DBTracker.Core.Enums;
using DBTracker.Core.Services;
using Xunit;

namespace DBTracker.UnitTests;

/// <summary>G12/U17: a choosable matching rule, a round-trippable inventory export, and correction downloads that carry the row data.</summary>
public class ImportMatchingAndCorrectionsTests
{
    private static DatabaseInstance Stored(int id, string name, bool archived = false) =>
        new() { Id = id, Name = name, Engine = DatabaseEngine.MySql, Region = "eu-west", IsArchived = archived };

    private static Dictionary<string, DatabaseInstance> ByName(params DatabaseInstance[] items) =>
        items.Where(i => !i.IsArchived).ToDictionary(i => i.Name, StringComparer.OrdinalIgnoreCase);

    private static Dictionary<int, DatabaseInstance> ById(params DatabaseInstance[] items) => items.ToDictionary(i => i.Id);

    [Fact]
    public void MatchById_RenamesTheMatchedInstance()
    {
        var a = Stored(5, "OLD-NAME");

        var preview = InstanceCsvImporter.ParseForImport("Id,Name,Engine\n5,NEW-NAME,MySql\n", ByName(a), matchRule: ImportMatchRule.Id, existingById: ById(a));

        var row = Assert.Single(preview.Rows);
        Assert.Equal(ImportRowPlan.Update, row.Plan);
        Assert.Equal(5, row.MatchedExistingId);
        Assert.Equal("NEW-NAME", row.Instance!.Name);
        Assert.Equal("eu-west", row.Instance.Region); // columns left out keep stored values

        var target = Stored(5, "OLD-NAME");
        InstanceCsvImporter.ApplyImportedFields(target, row.Instance, includeName: true);
        Assert.Equal("NEW-NAME", target.Name);
    }

    [Fact]
    public void MatchById_SameValues_IsSkip()
    {
        var a = Stored(5, "ARX-01");

        var preview = InstanceCsvImporter.ParseForImport("Id,Name,Engine\n5,ARX-01,MySql\n", ByName(a), matchRule: ImportMatchRule.Id, existingById: ById(a));

        Assert.Equal(ImportRowPlan.Skip, Assert.Single(preview.Rows).Plan);
    }

    [Fact]
    public void MatchById_EmptyId_CreatesUnlessTheNameIsTaken()
    {
        var a = Stored(5, "ARX-01");

        var preview = InstanceCsvImporter.ParseForImport("Id,Name,Engine\n,ARX-NEW,MySql\n,arx-01,MySql\n", ByName(a), matchRule: ImportMatchRule.Id, existingById: ById(a));

        Assert.Equal(ImportRowPlan.Create, preview.Rows[0].Plan);
        Assert.Equal(ImportRowPlan.Ambiguous, preview.Rows[1].Plan);
        Assert.Contains("already exists as instance #5", preview.Rows[1].AmbiguityReason);
    }

    [Fact]
    public void MatchById_RenameOntoAnotherInstancesName_IsAmbiguous()
    {
        var a = Stored(5, "ARX-01");
        var b = Stored(6, "ARX-02");

        var preview = InstanceCsvImporter.ParseForImport("Id,Name,Engine\n5,ARX-02,MySql\n", ByName(a, b), matchRule: ImportMatchRule.Id, existingById: ById(a, b));

        Assert.Contains("instance #6 already has that name", Assert.Single(preview.Rows).AmbiguityReason);
    }

    [Fact]
    public void MatchById_UnknownOrArchivedOrRepeatedId_IsNotApplied()
    {
        var a = Stored(5, "ARX-01");
        var gone = Stored(8, "GONE", archived: true);

        var preview = InstanceCsvImporter.ParseForImport("Id,Name,Engine\n99,X,MySql\n8,GONE,MySql\n5,ARX-01,MySql\n5,ARX-01B,MySql\n",
            ByName(a, gone), matchRule: ImportMatchRule.Id, existingById: ById(a, gone));

        Assert.Contains("No instance has Id 99", Assert.Single(preview.Errors).Message);
        Assert.Contains("archived", preview.Rows.Single(r => r.RowNumber == 3).AmbiguityReason);
        Assert.Contains("Id 5 appears more than once", preview.Rows.Single(r => r.RowNumber == 5).AmbiguityReason);
    }

    [Fact]
    public void MatchById_WithoutAnIdColumn_IsAHeaderError()
    {
        var preview = InstanceCsvImporter.ParseForImport("Name,Engine\nX,MySql\n", ByName(), matchRule: ImportMatchRule.Id, existingById: ById());

        Assert.Contains("needs an Id column", Assert.Single(preview.Errors).Message);
    }

    [Fact]
    public void MatchByName_NeverRenames()
    {
        var a = Stored(5, "ARX-01");

        var row = Assert.Single(InstanceCsvImporter.ParseForImport("Name,Engine,Region\narx-01,MySql,us-east\n", ByName(a)).Rows);
        var target = Stored(5, "ARX-01");
        InstanceCsvImporter.ApplyImportedFields(target, row.Instance!);

        Assert.Equal("ARX-01", target.Name);
        Assert.Equal("us-east", target.Region);
    }

    [Fact]
    public void InventoryExport_ReimportsByIdAsUnchanged()
    {
        var a = new DatabaseInstance
        {
            Id = 3, Name = "ARX, \"quoted\"", Engine = DatabaseEngine.SqlServer, Notes = "line one\nline two", OwnerTeamId = 4,
            ConnectivityPortOverride = 1433, RetentionPolicyDays = 30, Criticality = InstanceCriticality.High
        };
        var teams = new Dictionary<int, string> { [4] = "Platform Team" };

        var csv = InstanceCsvImporter.ExportCsv([a], teams);
        var preview = InstanceCsvImporter.ParseForImport(csv, ByName(a), teamIdsByName: new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase) { ["Platform Team"] = 4 },
            matchRule: ImportMatchRule.Id, existingById: ById(a));

        Assert.Empty(preview.Errors);
        Assert.Equal(ImportRowPlan.Skip, Assert.Single(preview.Rows).Plan);
    }

    [Fact]
    public void Corrections_CarryTheOriginalCells_AndTheReason_ForEachRejectedRow()
    {
        var csv = "Name,Engine,Notes\nGOOD,MySql,ok\nBAD,Nope,\"has, a comma\"\n";
        var preview = InstanceCsvImporter.ParseForImport(csv, ByName());

        var corrections = ImportCorrections.BuildCsv(csv, preview.Errors.Select(e => (e.RowNumber, e.Message)));
        var records = CsvRecordReader.Split(corrections);

        Assert.Equal(["ImportError", "SourceRow", "Name", "Engine", "Notes"], records[0]);
        var row = Assert.Single(records.Skip(1));
        Assert.Contains("Unrecognized engine 'Nope'", row[0]);
        Assert.Equal("3", row[1]);
        Assert.Equal(["BAD", "Nope", "has, a comma"], row.Skip(2));
    }

    [Fact]
    public void Corrections_FixedAndUploadedAgain_ImportWithTheExtraColumnsIgnored()
    {
        var csv = "Name,Engine\nBAD,Nope\n";
        var preview = InstanceCsvImporter.ParseForImport(csv, ByName());
        var corrections = ImportCorrections.BuildCsv(csv, preview.Errors.Select(e => (e.RowNumber, e.Message)));

        var fixedFile = corrections.Replace(",Nope", ",MySql");
        var again = InstanceCsvImporter.ParseForImport(fixedFile, ByName());

        Assert.Empty(again.Errors);
        Assert.Equal(ImportRowPlan.Create, Assert.Single(again.Rows).Plan);
    }

    [Fact]
    public void Corrections_PadShortRows_AndJoinSeveralMessagesForOneRow()
    {
        var csv = "A,B,C\nx\n";

        var records = CsvRecordReader.Split(ImportCorrections.BuildCsv(csv, [(2, "First."), (2, "Second.")]));

        Assert.Equal(["First. Second.", "2", "x", "", ""], records[1]);
    }

    [Fact]
    public void CsvLine_QuotesOnlyWhenNeeded()
    {
        Assert.Equal("a,\"b,c\",\"d\"\"e\",\"f\ng\"", CsvRecordReader.Line(["a", "b,c", "d\"e", "f\ng"]));
    }
}
