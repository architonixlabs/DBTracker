using DBTracker.Core.Enums;
using DBTracker.Core.Services;
using Xunit;

namespace DBTracker.UnitTests;

public class ScanActivityReportBuilderTests
{
    private static readonly DateTime Now = new(2026, 11, 10, 9, 0, 0, DateTimeKind.Utc);

    private static IReadOnlyList<ScanActivityRow> Build(IEnumerable<ScanActivityInput> inputs, int intervalMinutes = 15, int staleMultiplier = 3) =>
        ScanActivityReportBuilder.Build(inputs, intervalMinutes, staleMultiplier, Now);

    [Fact]
    public void ExcludesInstancesWithNoBackupFileLocation()
    {
        var inputs = new[]
        {
            new ScanActivityInput("SCANNED", @"\\server\backups", Now, ScanStatus.Success, ""),
            new ScanActivityInput("NOT-CONFIGURED", "", null, ScanStatus.NeverScanned, ""),
        };

        var row = Assert.Single(Build(inputs));
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

        var rows = Build(inputs);

        Assert.Equal("ERRORED", rows[0].InstanceName);
        Assert.Equal("path unreachable", rows[0].LastScanError);
        Assert.Equal(ScannerFreshness.Error, rows[0].Scanner.Freshness);
    }

    [Fact]
    public void StaleScanSortsBeforeHealthyScan()
    {
        var inputs = new[]
        {
            new ScanActivityInput("HEALTHY", @"\\server\a", Now.AddMinutes(-5), ScanStatus.Success, ""),
            new ScanActivityInput("STALE", @"\\server\b", Now.AddHours(-2), ScanStatus.Success, ""),
        };

        // interval 15 min, stale multiplier 3 => stale after 45 min.
        var rows = Build(inputs, intervalMinutes: 15, staleMultiplier: 3);

        Assert.Equal("STALE", rows[0].InstanceName);
        Assert.Equal(ScannerFreshness.Stale, rows[0].Scanner.Freshness);
        Assert.Equal(ScannerFreshness.Healthy, rows[1].Scanner.Freshness);
    }

    [Fact]
    public void NeverScannedSortsFirstAlongsideErrorAndStale()
    {
        var inputs = new[]
        {
            new ScanActivityInput("HEALTHY", @"\\server\a", Now, ScanStatus.Success, ""),
            new ScanActivityInput("NEVER", @"\\server\b", null, ScanStatus.NeverScanned, ""),
        };

        var rows = Build(inputs);

        Assert.Equal("NEVER", rows[0].InstanceName);
        Assert.Equal(ScannerFreshness.NeverScanned, rows[0].Scanner.Freshness);
    }
}
