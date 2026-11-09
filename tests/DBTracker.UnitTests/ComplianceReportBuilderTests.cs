using DBTracker.Core.Entities;
using DBTracker.Core.Enums;
using DBTracker.Core.Services;
using Xunit;

namespace DBTracker.UnitTests;

public class ComplianceReportBuilderTests
{
    private static readonly DateTime AsOf = new(2026, 11, 10, 9, 0, 0, DateTimeKind.Utc);

    private static DatabaseInstance Instance(string name, string company = "Acme", string dept = "Ops") => new()
    {
        Id = 1,
        Name = name,
        Company = company,
        Department = dept
    };

    [Fact]
    public void AllOnTrack_ReportsAllClear()
    {
        var rows = new[]
        {
            new ComplianceReportRow(Instance("A"), BackupHealth.OnTrack, AsOf.AddHours(-2), true),
            new ComplianceReportRow(Instance("B"), BackupHealth.OnTrack, AsOf.AddHours(-3), false),
        };

        var body = ComplianceReportBuilder.BuildPlainTextBody(rows, AsOf);
        var subject = ComplianceReportBuilder.BuildSubject(rows, AsOf);

        Assert.Contains("No instances currently at risk", body);
        Assert.Contains("all clear", subject);
    }

    [Fact]
    public void FailedAndOverdue_ListedInAttentionSection_FailedFirst()
    {
        var rows = new[]
        {
            new ComplianceReportRow(Instance("OVERDUE-ONE"), BackupHealth.Overdue, AsOf.AddDays(-3), false),
            new ComplianceReportRow(Instance("FAILED-ONE"), BackupHealth.Failed, AsOf.AddHours(-1), false),
            new ComplianceReportRow(Instance("HEALTHY-ONE"), BackupHealth.OnTrack, AsOf.AddHours(-1), true),
        };

        var body = ComplianceReportBuilder.BuildPlainTextBody(rows, AsOf);
        var subject = ComplianceReportBuilder.BuildSubject(rows, AsOf);

        Assert.Contains("FAILED-ONE", body);
        Assert.Contains("OVERDUE-ONE", body);
        Assert.DoesNotContain("HEALTHY-ONE", body);
        Assert.Contains("2 instance(s) need attention", subject);

        // Failed should be listed before Overdue (worse first).
        Assert.True(body.IndexOf("FAILED-ONE", StringComparison.Ordinal) < body.IndexOf("OVERDUE-ONE", StringComparison.Ordinal));
    }

    [Fact]
    public void NeverBackedUp_CountedAsAtRisk_WithNeverLabel()
    {
        var rows = new[]
        {
            new ComplianceReportRow(Instance("NEVER-ONE"), BackupHealth.NeverBackedUp, null, false),
        };

        var body = ComplianceReportBuilder.BuildPlainTextBody(rows, AsOf);

        Assert.Contains("NEVER-ONE", body);
        Assert.Contains("never", body);
    }

    [Fact]
    public void RestoreTestedCount_ReflectsRows()
    {
        var rows = new[]
        {
            new ComplianceReportRow(Instance("A"), BackupHealth.OnTrack, AsOf, true),
            new ComplianceReportRow(Instance("B"), BackupHealth.OnTrack, AsOf, true),
            new ComplianceReportRow(Instance("C"), BackupHealth.OnTrack, AsOf, false),
        };

        var body = ComplianceReportBuilder.BuildPlainTextBody(rows, AsOf);

        Assert.Contains("Restore tested:    2 of 3", body);
    }
}
