using DBTracker.Core.Enums;
using DBTracker.Core.Services;
using Xunit;

namespace DBTracker.UnitTests;

public class InstanceCsvImporterTests
{
    [Fact]
    public void ValidRow_IsParsed_WithDefaultsForOptionalFields()
    {
        var csv = "Name,Engine\nNEW-INSTANCE,PostgreSql\n";

        var result = InstanceCsvImporter.Parse(csv);

        Assert.Empty(result.Errors);
        var row = Assert.Single(result.Valid);
        Assert.Equal("NEW-INSTANCE", row.Name);
        Assert.Equal(DatabaseEngine.PostgreSql, row.Engine);
        Assert.Equal(InstanceRole.Active, row.Role);
        Assert.Equal(EnvironmentTier.Production, row.Environment);
        Assert.Equal(24, row.ExpectedBackupIntervalHours);
        Assert.Null(row.RetentionPolicyDays);
    }

    [Fact]
    public void FullRow_AllColumnsMapped()
    {
        var csv = "Name,Engine,Version,Application,Company,Department,ContactName,ContactEmail,ContactPhone,Role,Environment,DatabaseIp,ServerIp,ExpectedBackupIntervalHours,BackupFileLocation,RetentionPolicyDays,Tags,RunbookUrl,Notes\n" +
                  "ARX-01,SqlServer,2022,ArxMail,Architonix Labs,Platform,Priya Nair,priya@architonixlabs.com,555-0101,Passive,DisasterRecovery,10.0.0.1,10.0.0.2,48,server-backups-share,90,tag1;tag2,https://runbook,Some notes\n";

        var row = Assert.Single(InstanceCsvImporter.Parse(csv).Valid);

        Assert.Equal("ARX-01", row.Name);
        Assert.Equal(DatabaseEngine.SqlServer, row.Engine);
        Assert.Equal("2022", row.Version);
        Assert.Equal("ArxMail", row.Application);
        Assert.Equal(InstanceRole.Passive, row.Role);
        Assert.Equal(EnvironmentTier.DisasterRecovery, row.Environment);
        Assert.Equal(48, row.ExpectedBackupIntervalHours);
        Assert.Equal(90, row.RetentionPolicyDays);
        Assert.Equal("Some notes", row.Notes);
    }

    [Fact]
    public void MissingName_IsReportedAsError_AndRowSkipped()
    {
        var csv = "Name,Engine\n,PostgreSql\n";

        var result = InstanceCsvImporter.Parse(csv);

        Assert.Empty(result.Valid);
        var error = Assert.Single(result.Errors);
        Assert.Equal(2, error.RowNumber);
        Assert.Contains("Name is required", error.Message);
    }

    [Fact]
    public void UnrecognizedEngine_IsReportedAsError()
    {
        var csv = "Name,Engine\nX,NotARealEngine\n";

        var result = InstanceCsvImporter.Parse(csv);

        Assert.Empty(result.Valid);
        Assert.Contains(result.Errors, e => e.Message.Contains("Unrecognized engine"));
    }

    [Fact]
    public void MissingRequiredHeaderColumns_ReportsSingleHeaderError()
    {
        var csv = "Name,Application\nX,App1\n";

        var result = InstanceCsvImporter.Parse(csv);

        Assert.Empty(result.Valid);
        var error = Assert.Single(result.Errors);
        Assert.Contains("Header row must include", error.Message);
    }

    [Fact]
    public void HeaderIsCaseInsensitive_AndColumnOrderIndependent()
    {
        var csv = "engine,name\nMySql,REORDERED\n";

        var row = Assert.Single(InstanceCsvImporter.Parse(csv).Valid);

        Assert.Equal("REORDERED", row.Name);
        Assert.Equal(DatabaseEngine.MySql, row.Engine);
    }

    [Fact]
    public void QuotedValueWithEmbeddedComma_IsParsedAsOneField()
    {
        var csv = "Name,Engine,Notes\nX,PostgreSql,\"has, a comma\"\n";

        var row = Assert.Single(InstanceCsvImporter.Parse(csv).Valid);

        Assert.Equal("has, a comma", row.Notes);
    }

    [Fact]
    public void MultipleRows_ValidAndInvalidMixed_BothReported()
    {
        var csv = "Name,Engine\nGOOD,PostgreSql\n,MySql\nALSO-GOOD,Oracle\n";

        var result = InstanceCsvImporter.Parse(csv);

        Assert.Equal(2, result.Valid.Count);
        Assert.Single(result.Errors);
    }

    [Fact]
    public void TemplateCsv_StartsWithRequiredColumns()
    {
        var template = InstanceCsvImporter.TemplateCsv();

        Assert.StartsWith("Name,Engine,", template);
    }
}
