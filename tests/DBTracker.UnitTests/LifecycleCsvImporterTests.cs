using DBTracker.Core.Entities;
using DBTracker.Core.Enums;
using DBTracker.Core.Services;
using Xunit;

namespace DBTracker.UnitTests;

/// <summary>G13: CSV imports for the release catalogue, advisories and version metadata.</summary>
public class LifecycleCsvImporterTests
{
    private static readonly DateTime Now = new(2026, 11, 10, 0, 0, 0, DateTimeKind.Utc);

    // ---- Release catalogue ----

    [Fact]
    public void Catalogue_TemplateParses_WithNoRowsBecauseTheExampleIsCommentedOut()
    {
        var preview = ReleaseCatalogCsvImporter.ParseForImport(ReleaseCatalogCsvImporter.TemplateCsv(), [], Now);
        Assert.Empty(preview.Rows);
        Assert.Empty(preview.Errors);
    }

    [Fact]
    public void Catalogue_TemplateExampleRow_ParsesCleanlyOnceUncommented()
    {
        var lines = ReleaseCatalogCsvImporter.TemplateCsv().Split('\n');
        var csv = lines[0] + "\n" + lines[2].TrimStart('#') + "\n";

        var preview = ReleaseCatalogCsvImporter.ParseForImport(csv, [], Now);

        Assert.Empty(preview.Errors);
        var row = Assert.Single(preview.Rows);
        Assert.Equal(ImportRowPlan.Create, row.Plan);
        Assert.Equal(new DateTime(2028, 11, 9, 0, 0, 0, DateTimeKind.Utc), row.Item!.MainstreamSupportEndUtc);
        Assert.Equal("https://www.postgresql.org/docs/release/16.0/", row.Item.ReleaseNotesUrl);
    }

    [Fact]
    public void Catalogue_RowErrors_AreReportedPerRow_AndValidRowsStillPreview()
    {
        const string csv = "Engine,Family,ReleaseLabel,Major,MainstreamSupportEnd,SourceUrl,Channel\n" +
                           "PostgreSql,PostgreSQL,PG 16,16,2028-11-09,https://example.com,GA\n" +
                           "Nope,PostgreSQL,PG 15,15,,,\n" +
                           "PostgreSql,PostgreSQL,PG 14,fourteen,2026-13-40,ftp://x,Beta\n";

        var preview = ReleaseCatalogCsvImporter.ParseForImport(csv, [], Now);

        Assert.Single(preview.Rows);
        Assert.Equal(2, preview.Errors.Count);
        Assert.Equal(3, preview.Errors[0].RowNumber);
        var row4 = preview.Errors[1].Message;
        Assert.Contains("Major", row4);
        Assert.Contains("MainstreamSupportEnd", row4);
        Assert.Contains("SourceUrl", row4);
        Assert.Contains("Channel", row4);
    }

    [Fact]
    public void Catalogue_ReimportOfTheSameFacts_IsSkip_AndChangedFactsAreUpdate()
    {
        var existing = new ReleaseCatalogEntry
        {
            Id = 7, Engine = DatabaseEngine.PostgreSql, Family = "PostgreSQL", ReleaseLabel = "PG 16", Major = 16,
            MainstreamSupportEndUtc = new DateTime(2028, 11, 9, 0, 0, 0, DateTimeKind.Utc)
        };

        var same = ReleaseCatalogCsvImporter.ParseForImport("Engine,Family,ReleaseLabel,MainstreamSupportEnd\npostgresql,postgresql,pg 16,2028-11-09\n", [existing], Now);
        Assert.Equal(ImportRowPlan.Skip, Assert.Single(same.Rows).Plan);

        var changed = ReleaseCatalogCsvImporter.ParseForImport("Engine,Family,ReleaseLabel,MainstreamSupportEnd\nPostgreSql,PostgreSQL,PG 16,2028-12-01\n", [existing], Now);
        var row = Assert.Single(changed.Rows);
        Assert.Equal(ImportRowPlan.Update, row.Plan);
        Assert.Equal(7, row.MatchedExistingId);
        Assert.Equal(16, row.Item!.Major); // column not in the file keeps the stored value
    }

    [Fact]
    public void Catalogue_EntryUnderAnActiveOverride_IsNotOverwritten()
    {
        var existing = new ReleaseCatalogEntry
        {
            Id = 3, Engine = DatabaseEngine.SqlServer, Family = "SQL Server", ReleaseLabel = "2019",
            IsManualOverride = true, OverrideReason = "Vendor page republished", OverrideExpiresUtc = Now.AddDays(10)
        };

        var preview = ReleaseCatalogCsvImporter.ParseForImport("Engine,Family,ReleaseLabel,Major\nSqlServer,SQL Server,2019,15\n", [existing], Now);

        var row = Assert.Single(preview.Rows);
        Assert.Equal(ImportRowPlan.Ambiguous, row.Plan);
        Assert.Contains("manual override", row.Detail);
    }

    [Fact]
    public void Catalogue_DuplicateRowInFile_IsAmbiguous_AndCommentAndBlankLinesAreIgnored()
    {
        const string csv = "Engine,Family,ReleaseLabel\n# note\n\nMySql,MySQL,8.4 LTS\nMySql,MySQL,8.4 lts\n";
        var preview = ReleaseCatalogCsvImporter.ParseForImport(csv, [], Now);

        Assert.Equal([ImportRowPlan.Create, ImportRowPlan.Ambiguous], preview.Rows.Select(r => r.Plan));
        Assert.Empty(preview.Errors);
    }

    [Fact]
    public void Catalogue_MissingRequiredHeader_IsAFileError()
    {
        var preview = ReleaseCatalogCsvImporter.ParseForImport("Engine,Family\nMySql,MySQL\n", [], Now);
        Assert.Equal(1, Assert.Single(preview.Errors).RowNumber);
    }

    // ---- Advisories ----

    [Fact]
    public void Advisory_CreateThenReimport_IsSkip_AndQuotedDescriptionKeepsCommas()
    {
        const string csv = "Engine,Family,VendorAdvisoryId,Severity,Description,AffectedMinBuild,AffectedMaxBuild\n" +
                           "MySql,MySQL,CVE-2026-1,High,\"Affects auth, replication\",8.0.0,8.0.40\n";

        var first = AdvisoryCsvImporter.ParseForImport(csv, []);
        var created = Assert.Single(first.Rows);
        Assert.Equal(ImportRowPlan.Create, created.Plan);
        Assert.Equal("Affects auth, replication", created.Item!.Description);

        var stored = created.Item;
        stored.Id = 11;
        var again = AdvisoryCsvImporter.ParseForImport(csv, [stored]);
        Assert.Equal(ImportRowPlan.Skip, Assert.Single(again.Rows).Plan);
    }

    [Fact]
    public void Advisory_InvertedRangeAndBadSeverity_AreRowErrors()
    {
        const string csv = "Engine,Family,VendorAdvisoryId,Severity,AffectedMinBuild,AffectedMaxBuild\nMySql,MySQL,X-1,Urgent,8.0.40,8.0.1\n";
        var error = Assert.Single(AdvisoryCsvImporter.ParseForImport(csv, []).Errors).Message;
        Assert.Contains("Severity", error);
        Assert.Contains("higher than", error);
    }

    [Fact]
    public void Advisory_TemplateExampleParses()
    {
        var lines = AdvisoryCsvImporter.TemplateCsv().Split('\n');
        var preview = AdvisoryCsvImporter.ParseForImport(lines[0] + "\n" + lines[2].TrimStart('#') + "\n", []);
        Assert.Empty(preview.Errors);
        Assert.Equal(AdvisorySeverity.High, Assert.Single(preview.Rows).Item!.Severity);
    }

    // ---- Version metadata ----

    private static readonly Dictionary<string, int> Instances = new(StringComparer.OrdinalIgnoreCase) { ["ARXMAIL-PROD-01"] = 1 };
    private static readonly Dictionary<string, string> Blocked = new(StringComparer.OrdinalIgnoreCase) { ["OLD-DB"] = "'OLD-DB' is archived." };

    [Fact]
    public void Observation_SuccessAndPermissionFailure_BothImport()
    {
        const string csv = "Name,ObservedAt,Outcome,RawVersionText,FailureDetail\n" +
                           "arxmail-prod-01,2026-11-10 06:00,Success,16.4,\n" +
                           "ARXMAIL-PROD-01,2026-11-11 06:00,PermissionDenied,,VIEW SERVER STATE denied\n";

        var preview = VersionObservationCsvImporter.ParseForImport(csv, Instances, Blocked, new HashSet<string>());

        Assert.Empty(preview.Errors);
        Assert.Equal(2, preview.CreateCount);
        Assert.All(preview.Rows, r => Assert.Equal(1, r.Item!.DatabaseInstanceId));
        Assert.Equal(VersionObservationSource.CsvImport, preview.Rows[1].Item!.Source);
        Assert.Equal(ObservationOutcome.PermissionDenied, preview.Rows[1].Item!.Outcome);
    }

    [Fact]
    public void Observation_AlreadyStored_IsSkip()
    {
        var key = VersionObservationCsvImporter.Key(1, new DateTime(2026, 11, 10, 6, 0, 0, DateTimeKind.Utc), ObservationOutcome.Success, "16.4");
        var preview = VersionObservationCsvImporter.ParseForImport("Name,ObservedAt,RawVersionText\nARXMAIL-PROD-01,2026-11-10 06:00,16.4\n",
            Instances, Blocked, new HashSet<string> { key });
        Assert.Equal(ImportRowPlan.Skip, Assert.Single(preview.Rows).Plan);
    }

    [Fact]
    public void Observation_MissingTimeUnknownInstanceAndMissingDetail_AreErrors_ArchivedIsNotImported()
    {
        const string csv = "Name,ObservedAt,Outcome,RawVersionText,FailureDetail\n" +
                           "ARXMAIL-PROD-01,,Success,16.4,\n" +
                           "NO-SUCH-DB,2026-11-10 06:00,Success,16.4,\n" +
                           "ARXMAIL-PROD-01,2026-11-10 07:00,Unreachable,,\n" +
                           "OLD-DB,2026-11-10 06:00,Success,16.4,\n";

        var preview = VersionObservationCsvImporter.ParseForImport(csv, Instances, Blocked, new HashSet<string>());

        Assert.Equal(3, preview.Errors.Count);
        Assert.Contains("ObservedAt is required", preview.Errors[0].Message);
        Assert.Contains("No instance", preview.Errors[1].Message);
        Assert.Contains("FailureDetail", preview.Errors[2].Message);
        Assert.Equal(ImportRowPlan.Ambiguous, Assert.Single(preview.Rows).Plan);
    }
}
