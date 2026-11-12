using DBTracker.Core.Entities;
using DBTracker.Core.Enums;
using DBTracker.Core.Services;
using Xunit;

namespace DBTracker.UnitTests;

public class EvidenceCsvImportTests
{
    private static readonly DateTime Now = new(2026, 11, 11, 12, 0, 0, DateTimeKind.Utc);
    private static readonly Dictionary<string, int> Instances = new(StringComparer.OrdinalIgnoreCase) { ["PROD-SQL-01"] = 7 };
    private static readonly Dictionary<string, string> Blocked = new(StringComparer.OrdinalIgnoreCase) { ["OLD-SQL"] = "'OLD-SQL' is archived." };

    private const string ResultsHeader = "Instance,CheckType,RanAtUtc,Outcome,NodeOrDeployment,Source,SourceReference,ExpectedIntervalHours\n";

    [Fact]
    public void Results_ValidRow_IsCreate_WithUtcTime()
    {
        var csv = ResultsHeader + "prod-sql-01,IntegrityCheck,2026-11-01T02:00:00Z,Passed,node-a,ExternalMonitoringLink,https://mon/1,168\n";

        var preview = MaintenanceEvidenceCsvImporter.Parse(csv, Instances, Blocked, [], Now);

        var row = Assert.Single(preview.Rows);
        Assert.Equal(EvidenceImportRowPlan.Create, row.Plan);
        Assert.Equal(7, row.Record!.DatabaseInstanceId);
        Assert.Equal(new DateTime(2026, 11, 1, 2, 0, 0, DateTimeKind.Utc), row.Record.RanAtUtc);
        Assert.Equal(168, row.Record.ExpectedIntervalHours);
        Assert.Empty(preview.Errors);
    }

    [Fact]
    public void Results_CommentRowsAndTemplate_AreIgnored()
    {
        var preview = MaintenanceEvidenceCsvImporter.Parse(MaintenanceEvidenceCsvImporter.TemplateCsv(), Instances, Blocked, [], Now);

        Assert.Empty(preview.Rows);
        Assert.Empty(preview.Errors);
    }

    [Fact]
    public void Results_AlreadyRecorded_IsSkipped_SoReimportIsSafe()
    {
        var existing = new MaintenanceEvidenceRecord
        {
            DatabaseInstanceId = 7, CheckType = MaintenanceCheckType.IntegrityCheck, NodeOrDeploymentLabel = "NODE-A",
            RanAtUtc = new DateTime(2026, 11, 1, 2, 0, 0, DateTimeKind.Utc), Outcome = MaintenanceOutcome.Passed
        };
        var csv = ResultsHeader + "PROD-SQL-01,IntegrityCheck,2026-11-01T02:00:00Z,Passed,node-a,ExternalMonitoringLink,https://mon/1,\n";

        var preview = MaintenanceEvidenceCsvImporter.Parse(csv, Instances, Blocked, [existing], Now);

        Assert.Equal(EvidenceImportRowPlan.Skip, Assert.Single(preview.Rows).Plan);
    }

    [Fact]
    public void Results_SameRunWithDifferentOutcome_IsAConflict_NotAnOverwrite()
    {
        var existing = new MaintenanceEvidenceRecord
        {
            DatabaseInstanceId = 7, CheckType = MaintenanceCheckType.IntegrityCheck,
            RanAtUtc = new DateTime(2026, 11, 1, 2, 0, 0, DateTimeKind.Utc), Outcome = MaintenanceOutcome.Failed
        };
        var csv = ResultsHeader + "PROD-SQL-01,IntegrityCheck,2026-11-01T02:00:00Z,Passed,,ManuallyEntered,,\n";

        var row = Assert.Single(MaintenanceEvidenceCsvImporter.Parse(csv, Instances, Blocked, [existing], Now).Rows);

        Assert.Equal(EvidenceImportRowPlan.Conflict, row.Plan);
        Assert.Contains("never overwritten", row.Reason);
    }

    [Fact]
    public void Results_RepeatedRowInFile_IsAConflict()
    {
        var line = "PROD-SQL-01,FailoverDrill,2026-11-01T02:00:00Z,Passed,,ManuallyEntered,,\n";

        var preview = MaintenanceEvidenceCsvImporter.Parse(ResultsHeader + line + line, Instances, Blocked, [], Now);

        Assert.Equal([EvidenceImportRowPlan.Create, EvidenceImportRowPlan.Conflict], preview.Rows.Select(r => r.Plan));
    }

    [Fact]
    public void Results_BadRows_AreErrorsWithRowNumbers()
    {
        var csv = ResultsHeader +
                  "UNKNOWN-DB,IntegrityCheck,2026-11-01T02:00:00Z,Passed,,ManuallyEntered,,\n" +
                  "OLD-SQL,IntegrityCheck,2026-11-01T02:00:00Z,Passed,,ManuallyEntered,,\n" +
                  "PROD-SQL-01,Vacuum,yesterday,Maybe,,,,\n" +
                  "PROD-SQL-01,IntegrityCheck,2026-12-01T00:00:00Z,Passed,,TicketLink,,\n";

        var preview = MaintenanceEvidenceCsvImporter.Parse(csv, Instances, Blocked, [], Now);

        Assert.Empty(preview.Rows);
        Assert.Equal([2, 3, 4, 5], preview.Errors.Select(e => e.RowNumber));
        Assert.Contains("No instance named", preview.Errors[0].Message);
        Assert.Contains("archived", preview.Errors[1].Message);
        Assert.Contains("CheckType", preview.Errors[2].Message);
        Assert.Contains("Outcome", preview.Errors[2].Message);
        Assert.Contains("future", preview.Errors[3].Message);
        Assert.Contains("SourceReference is required", preview.Errors[3].Message);
    }

    [Fact]
    public void Results_MissingRequiredHeader_IsOneError()
    {
        var preview = MaintenanceEvidenceCsvImporter.Parse("Instance,CheckType\nPROD-SQL-01,IntegrityCheck\n", Instances, Blocked, [], Now);

        Assert.Single(preview.Errors);
        Assert.Contains("RanAtUtc", preview.Errors[0].Message);
    }

    [Fact]
    public void Results_SecretLookingNotes_AreRedacted()
    {
        var csv = "Instance,CheckType,RanAtUtc,Outcome,Source,Notes\nPROD-SQL-01,IntegrityCheck,2026-11-01T02:00:00Z,Passed,ManuallyEntered,password=hunter2\n";

        var row = Assert.Single(MaintenanceEvidenceCsvImporter.Parse(csv, Instances, Blocked, [], Now).Rows);

        Assert.DoesNotContain("hunter2", row.Record!.Notes);
    }

    private const string CapacityHeader = "Instance,MeasurementType,MeasuredAtUtc,Value,Unit,Source,MeasurementPeriod\n";

    [Fact]
    public void Capacity_ValidRow_IsCreate()
    {
        var csv = CapacityHeader + "PROD-SQL-01,Growth,2026-11-01T00:00:00Z,812.5,GB,Zabbix,point-in-time\n";

        var row = Assert.Single(CapacityMeasurementCsvImporter.Parse(csv, Instances, Blocked, [], Now).Rows);

        Assert.Equal(EvidenceImportRowPlan.Create, row.Plan);
        Assert.Equal(812.5, row.Record!.Value);
        Assert.Equal("Zabbix", row.Record.Source);
    }

    [Fact]
    public void Capacity_SameMeasurementAlreadyRecorded_IsSkipped_DifferentValueIsConflict()
    {
        var at = new DateTime(2026, 11, 1, 0, 0, 0, DateTimeKind.Utc);
        var existing = new CapacityMeasurement { DatabaseInstanceId = 7, MeasurementType = CapacityMeasurementType.Growth, MeasuredAtUtc = at, Value = 812.5, Unit = "gb" };

        var same = CapacityMeasurementCsvImporter.Parse(CapacityHeader + "PROD-SQL-01,Growth,2026-11-01T00:00:00Z,812.5,GB,Zabbix,\n", Instances, Blocked, [existing], Now);
        var different = CapacityMeasurementCsvImporter.Parse(CapacityHeader + "PROD-SQL-01,Growth,2026-11-01T00:00:00Z,900,GB,Zabbix,\n", Instances, Blocked, [existing], Now);

        Assert.Equal(EvidenceImportRowPlan.Skip, Assert.Single(same.Rows).Plan);
        Assert.Equal(EvidenceImportRowPlan.Conflict, Assert.Single(different.Rows).Plan);
    }

    [Fact]
    public void Capacity_MissingUnitSourceOrValue_IsAnError()
    {
        var csv = CapacityHeader + "PROD-SQL-01,Growth,2026-11-01T00:00:00Z,abc,,,\n";

        var preview = CapacityMeasurementCsvImporter.Parse(csv, Instances, Blocked, [], Now);

        var error = Assert.Single(preview.Errors);
        Assert.Contains("Value", error.Message);
        Assert.Contains("Unit is required", error.Message);
        Assert.Contains("Source is required", error.Message);
    }

    [Fact]
    public void Capacity_Template_ParsesToNothing()
    {
        var preview = CapacityMeasurementCsvImporter.Parse(CapacityMeasurementCsvImporter.TemplateCsv(), Instances, Blocked, [], Now);

        Assert.Empty(preview.Rows);
        Assert.Empty(preview.Errors);
    }
}
