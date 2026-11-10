using DBTracker.Core.Services;
using Xunit;

namespace DBTracker.UnitTests;

public class ArchivePreviewBuilderTests
{
    [Fact]
    public void NoScanNoAlertNoIncidents_AllLinesAreBenign()
    {
        var lines = ArchivePreviewBuilder.Build(new ArchiveImpactCounts(false, false, 0, false));

        Assert.All(lines, l => Assert.DoesNotContain("stop", l.Text));
    }

    [Fact]
    public void ScanConfigured_WarnsScanWillStop()
    {
        var lines = ArchivePreviewBuilder.Build(new ArchiveImpactCounts(true, false, 0, false));

        Assert.Contains(lines, l => l.Text.Contains("scan", System.StringComparison.OrdinalIgnoreCase) && l.Text.Contains("stop"));
    }

    [Fact]
    public void AlertableNow_WarnsAlertsStopImmediately()
    {
        var lines = ArchivePreviewBuilder.Build(new ArchiveImpactCounts(false, true, 0, false));

        Assert.Contains(lines, l => l.Text.Contains("alertable"));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(3)]
    public void OpenIncidentCount_IsReflectedInWording(int count)
    {
        var lines = ArchivePreviewBuilder.Build(new ArchiveImpactCounts(false, false, count, false));

        if (count == 0)
        {
            Assert.Contains(lines, l => l.Text.Contains("No open incident"));
        }
        else
        {
            Assert.Contains(lines, l => l.Text.Contains(count.ToString()));
        }
    }

    [Fact]
    public void IncludedInReports_WarnsItDropsOutOfNewReports()
    {
        var lines = ArchivePreviewBuilder.Build(new ArchiveImpactCounts(false, false, 0, true));

        Assert.Contains(lines, l => l.Text.Contains("drops out"));
    }
}
