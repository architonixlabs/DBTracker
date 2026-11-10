using DBTracker.Core.Enums;
using DBTracker.Core.Services;
using Xunit;

namespace DBTracker.UnitTests;

public class ScanActivityReportBuilderTests
{
    private static readonly DateTime Now = new(2026, 11, 10, 9, 0, 0, DateTimeKind.Utc);

    [Fact]
    public void ExcludesInstancesWithNoBackupFileLocation()
    {
        var inputs = new[]
        {
            new ScanActivityInput("SCANNED", @"\\server\backups", Now, ScanStatus.Success, ""),
            new ScanActivityInput("NOT-CONFIGURED", "", null, ScanStatus.NeverScanned, ""),
        };

        var row = Assert.Single(ScanActivityReportBuilder.Build(inputs));
        Assert.Equal("SCANNED", row.InstanceName);
    }

    [Fact]
    public void ErroredScansSortFirst()
    {
        var inputs = new[]
        {
            new ScanActivityInput("HEALTHY", @"\\server\a", Now, ScanStatus.Success, ""),
            new ScanActivityInput("ERRORED", @"\\server\b", Now, ScanStatus.Error, "path unreachable"),
        };

        var rows = ScanActivityReportBuilder.Build(inputs);

        Assert.Equal("ERRORED", rows[0].InstanceName);
        Assert.Equal("path unreachable", rows[0].LastScanError);
    }
}
