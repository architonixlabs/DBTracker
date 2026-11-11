using DBTracker.Core.Enums;
using DBTracker.Core.Services;
using Xunit;

namespace DBTracker.UnitTests;

public class TraySummaryBuilderTests
{
    private static readonly DateTime Now = new(2026, 11, 11, 9, 0, 0, DateTimeKind.Utc);

    private static InstanceListRow Row(int id, string name, BackupHealth health, EnvironmentTier env = EnvironmentTier.Production, DateTime? lastSuccess = null) =>
        new(id, name, DatabaseEngine.SqlServer, "16.0", "App", "Acme", "Ops", "", "Priya", "priya@acme.test",
            InstanceRole.Active, env, "10.0.0.1", health, lastSuccess, null, "", null, ScanStatus.NeverScanned, "");

    [Fact]
    public void Counts_EachRiskState_AndIgnoresOnTrack()
    {
        var rows = new[]
        {
            Row(1, "A", BackupHealth.Failed), Row(2, "B", BackupHealth.Failed),
            Row(3, "C", BackupHealth.Overdue),
            Row(4, "D", BackupHealth.NeverBackedUp),
            Row(5, "E", BackupHealth.OnTrack),
        };

        var summary = TraySummaryBuilder.Build(rows, "priya@acme.test", Now);

        Assert.Equal(2, summary.Failed);
        Assert.Equal(1, summary.Overdue);
        Assert.Equal(1, summary.NeverBackedUp);
        Assert.Equal(4, summary.AtRisk.Count);
        Assert.DoesNotContain(summary.AtRisk, r => r.Name == "E");
        Assert.Equal("priya@acme.test", summary.User);
        Assert.Equal("/", summary.DashboardUrl);
        Assert.Equal(Now, summary.GeneratedAtUtc);
    }

    [Fact]
    public void AtRisk_IsWorstFirst_LikeTheDashboard_ThenByName()
    {
        var rows = new[]
        {
            Row(1, "zeta", BackupHealth.Overdue),
            Row(2, "beta", BackupHealth.Failed),
            Row(3, "alpha", BackupHealth.NeverBackedUp),
            Row(4, "Alpha2", BackupHealth.Failed),
        };

        var names = TraySummaryBuilder.Build(rows, "u", Now).AtRisk.Select(r => r.Name).ToList();

        Assert.Equal(["Alpha2", "beta", "alpha", "zeta"], names);
    }

    [Fact]
    public void AtRisk_IsCappedAtTwenty_ButCountsCoverEveryInstance()
    {
        var rows = Enumerable.Range(1, 30).Select(i => Row(i, $"db{i:00}", BackupHealth.Failed)).ToList();

        var summary = TraySummaryBuilder.Build(rows, "u", Now);

        Assert.Equal(30, summary.Failed);
        Assert.Equal(TraySummaryBuilder.MaxAtRisk, summary.AtRisk.Count);
    }

    [Fact]
    public void AtRiskEntry_CarriesHealthEnvironmentAndUtcLastSuccess()
    {
        var lastSuccess = new DateTime(2026, 11, 9, 2, 0, 0, DateTimeKind.Unspecified);
        var rows = new[] { Row(7, "PROD-SQL", BackupHealth.Overdue, EnvironmentTier.Staging, lastSuccess), Row(8, "NEW", BackupHealth.NeverBackedUp) };

        var atRisk = TraySummaryBuilder.Build(rows, "u", Now).AtRisk;
        var overdue = atRisk.Single(r => r.Id == 7);

        Assert.Equal("Overdue", overdue.Health);
        Assert.Equal("Staging", overdue.Environment);
        Assert.Equal(DateTimeKind.Utc, overdue.LastSuccessUtc!.Value.Kind);
        Assert.Equal(lastSuccess.Ticks, overdue.LastSuccessUtc.Value.Ticks);
        Assert.Null(atRisk.Single(r => r.Id == 8).LastSuccessUtc);
    }

    [Fact]
    public void NoInstances_GivesZeroCounts()
    {
        var summary = TraySummaryBuilder.Build([], "u", Now);
        Assert.Equal(0, summary.Failed + summary.Overdue + summary.NeverBackedUp);
        Assert.Empty(summary.AtRisk);
    }
}
