using DBTracker.Core.Entities;
using DBTracker.Core.Enums;
using DBTracker.Core.Services;
using Xunit;

namespace DBTracker.UnitTests;

/// <summary>G11: snapshots freeze evidence level, recency and policy; Trends never uses a snapshot from before the period.</summary>
public class SnapshotEvidenceAndTrendsTests
{
    private static readonly DateTime AsOf = new(2026, 11, 11, 12, 0, 0, DateTimeKind.Utc);

    private static ComplianceSnapshotInput Input(string name, IEnumerable<BackupRecord> backups, IEnumerable<RestoreEvent> restores, string? policy = null, int? policyVersion = null, int? restoreOverride = null) =>
        new(new DatabaseInstance { Id = name.Length, Name = name, AppliedPolicyVersion = policyVersion, RestoreTestIntervalDaysOverride = restoreOverride, ExpectedBackupIntervalHours = 24 },
            BackupHealth.OnTrack, AsOf.AddHours(-3), backups.ToList(), restores.ToList(), policy);

    [Fact]
    public void Rows_FreezeEvidenceLevel_RecencyAndPolicy()
    {
        var rows = ComplianceSnapshotBuilder.BuildRows(
        [
            Input("A", [new BackupRecord { Status = BackupStatus.Success, TimestampUtc = AsOf.AddDays(-2) }],
                [new RestoreEvent { Outcome = RestoreOutcome.Success, TimestampUtc = AsOf.AddDays(-1) }], "Gold", 3),
            Input("BB", [new BackupRecord { Status = BackupStatus.Success, Source = BackupSource.Imported, TimestampUtc = AsOf.AddDays(-1) }],
                [new RestoreEvent { Outcome = RestoreOutcome.Success, Source = BackupSource.Imported, TimestampUtc = AsOf.AddHours(-1) }]),
            Input("CCC", [new BackupRecord { Status = BackupStatus.Success, Source = BackupSource.Discovered, TimestampUtc = AsOf.AddHours(-5) }],
                [new RestoreEvent { Outcome = RestoreOutcome.Success, TimestampUtc = AsOf.AddDays(-200) }], restoreOverride: 30),
        ], AsOf, 90);

        var a = rows.Single(r => r.InstanceName == "A");
        Assert.Equal(EvidenceLevel.RestoreTested, a.Evidence);
        Assert.Equal(RestoreTestRecency.Current, a.RestoreRecency);
        Assert.Equal(("Gold", 3), (a.PolicyName, a.PolicyVersion));
        Assert.Equal(90, a.RestoreTestIntervalDays);

        var b = rows.Single(r => r.InstanceName == "BB");
        Assert.Equal(EvidenceLevel.Imported, b.Evidence);
        Assert.False(b.RestoreTested);
        Assert.Equal(RestoreTestRecency.NeverTested, b.RestoreRecency);

        var c = rows.Single(r => r.InstanceName == "CCC");
        Assert.Equal(EvidenceLevel.Discovered, c.Evidence);
        Assert.Equal(RestoreTestRecency.Overdue, c.RestoreRecency);
        Assert.Equal(30, c.RestoreTestIntervalDays);
    }

    [Fact]
    public void Rows_IgnoreEventsAfterTheEffectiveTime()
    {
        var rows = ComplianceSnapshotBuilder.BuildRows(
            [Input("A", [new BackupRecord { Status = BackupStatus.Success, TimestampUtc = AsOf.AddDays(1) }], [new RestoreEvent { Outcome = RestoreOutcome.Success, TimestampUtc = AsOf.AddDays(1) }])],
            AsOf, 90);

        Assert.Null(rows[0].Evidence);
        Assert.False(rows[0].RestoreTested);
    }

    [Fact]
    public void LegacyJson_WithoutTheNewFields_StillDeserializes_WithThemNull()
    {
        const string legacy = "[{\"InstanceName\":\"OLD\",\"Company\":\"C\",\"Department\":\"D\",\"Role\":0,\"Environment\":0,\"LastBackupUtc\":null,\"Health\":0,\"RestoreTested\":true}]";

        var row = Assert.Single(ComplianceSnapshotBuilder.Deserialize(legacy));

        Assert.Equal("OLD", row.InstanceName);
        Assert.True(row.RestoreTested);
        Assert.Null(row.Evidence);
        Assert.Null(row.RestoreRecency);
        Assert.Null(row.InstanceId);
    }

    [Fact]
    public void RoundTrip_PreservesTheNewFields()
    {
        var rows = ComplianceSnapshotBuilder.BuildRows([Input("A", [new BackupRecord { Status = BackupStatus.Success, TimestampUtc = AsOf.AddDays(-2) }], [], "Gold", 2)], AsOf, 90);

        Assert.Equal(rows, ComplianceSnapshotBuilder.Deserialize(ComplianceSnapshotBuilder.Serialize(rows)));
    }

    [Fact]
    public void PolicyVersionLabel_ChangesWithPolicySettings_AndFitsTheColumn()
    {
        var gold2 = ComplianceSnapshotBuilder.BuildRows([Input("A", [], [], "Gold", 2)], AsOf, 90);
        var gold3 = ComplianceSnapshotBuilder.BuildRows([Input("A", [], [], "Gold", 3)], AsOf, 90);

        var label2 = ComplianceSnapshotBuilder.PolicyVersionLabel(gold2);
        Assert.StartsWith($"rules-v{ComplianceSnapshotBuilder.HealthRulesVersion} 1 policy ", label2);
        Assert.NotEqual(label2, ComplianceSnapshotBuilder.PolicyVersionLabel(gold3));
        Assert.Equal(label2, ComplianceSnapshotBuilder.PolicyVersionLabel(ComplianceSnapshotBuilder.BuildRows([Input("A", [], [], "Gold", 2)], AsOf, 90)));
        Assert.True(label2.Length <= 50);
        Assert.NotEqual("default-v1", label2);
    }

    [Fact]
    public void Provenance_CountsEvidenceLevels_AndStatesTheImportRule()
    {
        var rows = ComplianceSnapshotBuilder.BuildRows(
        [
            Input("A", [new BackupRecord { Status = BackupStatus.Success, Source = BackupSource.Imported, TimestampUtc = AsOf.AddDays(-1) }], []),
            Input("BB", [], []),
        ], AsOf, 90);

        var text = ComplianceSnapshotBuilder.DescribeProvenance(rows, "Compliance view");

        Assert.Contains("1 imported", text);
        Assert.Contains("1 none", text);
        Assert.Contains("never imported history", text);
        Assert.True(text.Length <= 500);
    }

    private static readonly DateTime From = new(2026, 10, 1, 0, 0, 0, DateTimeKind.Utc);
    private static readonly DateTime To = new(2026, 10, 31, 23, 59, 59, DateTimeKind.Utc);

    [Fact]
    public void SelectSnapshot_UsesTheLatestInsideThePeriod()
    {
        var choice = TrendsReportBuilder.SelectSnapshot(
            [new TrendSnapshotCandidate(1, From.AddDays(2)), new TrendSnapshotCandidate(2, From.AddDays(20)), new TrendSnapshotCandidate(3, To.AddDays(2))], From, To);

        Assert.Equal(2, choice.SnapshotId);
        Assert.Null(choice.MissingReason);
    }

    [Fact]
    public void SelectSnapshot_RejectsOneTakenBeforeThePeriodStarted()
    {
        var choice = TrendsReportBuilder.SelectSnapshot([new TrendSnapshotCandidate(1, From.AddDays(-3))], From, To);

        Assert.Null(choice.SnapshotId);
        Assert.Contains("before the period started", choice.MissingReason);
    }

    [Fact]
    public void SelectSnapshot_None_SaysNoneWasSaved()
    {
        Assert.Contains("No Compliance snapshot", TrendsReportBuilder.SelectSnapshot([], From, To).MissingReason);
    }

    private static TrendPeriodInput Period(IReadOnlyList<ComplianceSnapshotRow>? rows, IReadOnlyList<TrendPolicyChange>? changes = null, string? reason = null) =>
        new(From, To, rows, rows is null ? null : To, [], [], [], changes, reason);

    [Fact]
    public void Period_CountsRestoreRecency_FromTheSnapshot()
    {
        var rows = new[]
        {
            new ComplianceSnapshotRow("A", "", "", InstanceRole.Active, EnvironmentTier.Production, null, BackupHealth.OnTrack, true, 1, RestoreRecency: RestoreTestRecency.Current),
            new ComplianceSnapshotRow("B", "", "", InstanceRole.Active, EnvironmentTier.Production, null, BackupHealth.OnTrack, true, 2, RestoreRecency: RestoreTestRecency.Overdue),
            new ComplianceSnapshotRow("C", "", "", InstanceRole.Active, EnvironmentTier.Production, null, BackupHealth.OnTrack, false, 3, RestoreRecency: RestoreTestRecency.NeverTested),
        };

        var result = TrendsReportBuilder.BuildPeriod(Period(rows));

        Assert.Equal((1, 1, 1), (result.RestoreCurrentCount, result.RestoreOverdueCount, result.RestoreNeverTestedCount));
        Assert.Equal("B", Assert.Single(result.RestoreOverdueRows).InstanceName);
    }

    [Fact]
    public void Period_WithALegacySnapshot_LeavesRecencyUnrecorded()
    {
        var rows = new[] { new ComplianceSnapshotRow("A", "", "", InstanceRole.Active, EnvironmentTier.Production, null, BackupHealth.OnTrack, true) };

        var result = TrendsReportBuilder.BuildPeriod(Period(rows));

        Assert.Null(result.RestoreCurrentCount);
        Assert.Empty(result.RestoreOverdueRows);
    }

    [Fact]
    public void Period_ListsOnlyPolicyChangesInsideIt_AndCarriesTheMissingReason()
    {
        var changes = new[]
        {
            new TrendPolicyChange(From.AddDays(-1), "Gold", "sam", "before"),
            new TrendPolicyChange(From.AddDays(5), "Gold", "sam", "inside"),
        };

        var result = TrendsReportBuilder.BuildPeriod(Period(null, changes, "The latest snapshot was taken before the period started."));

        Assert.True(result.MissingEvidence);
        Assert.Equal("inside", Assert.Single(result.PolicyChanges).Summary);
        Assert.Contains("before the period started", result.MissingEvidenceReason);
    }
}
