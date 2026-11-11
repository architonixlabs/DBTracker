using DBTracker.Core.Entities;
using DBTracker.Core.Enums;
using DBTracker.Core.Services;
using Xunit;

namespace DBTracker.UnitTests;

/// <summary>G12: conflict review and merge (U13), before/after audit JSON (U19), archive preview (U14) and policy edit impact (U21).</summary>
public class EditAuditAndPolicyTests
{
    private static DatabaseInstance Instance(Action<DatabaseInstance>? change = null)
    {
        var i = new DatabaseInstance { Id = 1, Name = "ARX-01", Engine = DatabaseEngine.MySql, ContactName = "Priya", Notes = "n", Region = "eu-west" };
        change?.Invoke(i);
        return i;
    }

    [Fact]
    public void Conflict_DifferentFields_MergesKeepingBothChanges()
    {
        var opened = InstanceChangeLogBuilder.Fields(Instance());
        var theirs = Instance(i => i.ContactName = "Sam");
        var mine = Instance(i => i.Notes = "updated notes");

        var comparison = ConcurrentEditComparer.Compare(opened, theirs, mine);

        Assert.False(comparison.HasConflicts);
        Assert.Equal(["ContactName", "Notes"], comparison.Changed.Select(r => r.Field));
        Assert.True(ConcurrentEditComparer.TryMerge(comparison, theirs, mine));
        Assert.Equal("Sam", theirs.ContactName);
        Assert.Equal("updated notes", theirs.Notes);
    }

    [Fact]
    public void Conflict_SameFieldDifferentValues_CannotMerge()
    {
        var opened = InstanceChangeLogBuilder.Fields(Instance());
        var theirs = Instance(i => i.Region = "us-east");
        var mine = Instance(i => i.Region = "ap-south");

        var comparison = ConcurrentEditComparer.Compare(opened, theirs, mine);

        var row = Assert.Single(comparison.Changed);
        Assert.True(row.Conflicts);
        Assert.False(ConcurrentEditComparer.TryMerge(comparison, theirs, mine));
        Assert.Equal("us-east", theirs.Region);
    }

    [Fact]
    public void Conflict_SameChangeOnBothSides_IsNotAConflict()
    {
        var opened = InstanceChangeLogBuilder.Fields(Instance());

        var comparison = ConcurrentEditComparer.Compare(opened, Instance(i => i.ConnectivityPortOverride = 3307), Instance(i => i.ConnectivityPortOverride = 3307));

        Assert.False(comparison.HasConflicts);
    }

    [Fact]
    public void Merge_CopiesTypedValues_NotJustText()
    {
        var opened = InstanceChangeLogBuilder.Fields(Instance());
        var theirs = Instance(i => i.Notes = "theirs");
        var mine = Instance(i => { i.Criticality = InstanceCriticality.High; i.OwnerTeamId = 4; i.RetentionPolicyDays = 30; });

        Assert.True(ConcurrentEditComparer.TryMerge(ConcurrentEditComparer.Compare(opened, theirs, mine), theirs, mine));

        Assert.Equal(InstanceCriticality.High, theirs.Criticality);
        Assert.Equal(4, theirs.OwnerTeamId);
        Assert.Equal(30, theirs.RetentionPolicyDays);
    }

    [Fact]
    public void ChangeLog_NowCoversCriticalityAndConnectivityOverrides()
    {
        var changes = InstanceChangeLogBuilder.Diff(Instance(), Instance(i => { i.Criticality = InstanceCriticality.Low; i.ConnectivityPortOverride = 5433; }), "sam", DateTime.UtcNow);

        Assert.Equal(["Criticality", "ConnectivityPortOverride"], changes.Select(c => c.FieldName));
    }

    [Fact]
    public void AuditJson_FromChanges_HoldsOnlyChangedFields()
    {
        var changes = InstanceChangeLogBuilder.Diff(Instance(), Instance(i => i.ContactName = "Sam"), "sam", DateTime.UtcNow);

        var (before, after) = AuditDiffJson.FromChanges(changes);

        Assert.Equal("{\"ContactName\":\"Priya\"}", before);
        Assert.Equal("{\"ContactName\":\"Sam\"}", after);
        Assert.Equal((null, null), AuditDiffJson.FromChanges([]));
    }

    [Fact]
    public void AuditJson_ForCreate_LeavesOutEmptyFields_AndPairsForDisplay()
    {
        var json = AuditDiffJson.ForCreate(Instance());

        var pairs = AuditDiffJson.Pair(null, json);
        Assert.Contains(pairs, p => p.Field == "Name" && p.After == "ARX-01" && p.Before == "");
        Assert.DoesNotContain(pairs, p => p.Field == "ContactEmail");
    }

    [Fact]
    public void AuditJson_Pair_ReadsTheHandWrittenPolicyJson()
    {
        var pairs = AuditDiffJson.Pair("{\"ExpectedBackupIntervalHours\":24,\"RetentionPolicyDays\":null}", "{\"ExpectedBackupIntervalHours\":12,\"RetentionPolicyDays\":30}");

        Assert.Contains(("ExpectedBackupIntervalHours", "24", "12"), pairs);
        Assert.Contains(("RetentionPolicyDays", "", "30"), pairs);
        Assert.Equal([("", "not json")], AuditDiffJson.Describe("not json"));
    }

    [Fact]
    public void ArchivePreview_ReportLine_ReflectsTheRealSchedule_AndSnapshots()
    {
        var emailed = ArchivePreviewBuilder.Build(new ArchiveImpactCounts(false, false, 0, true, ScheduledReportIntervalDays: 7, DistributionListConfigured: true, SavedSnapshotCount: 2));
        var off = ArchivePreviewBuilder.Build(new ArchiveImpactCounts(false, false, 0, true, ScheduledReportIntervalDays: 0));
        var noList = ArchivePreviewBuilder.Build(new ArchiveImpactCounts(false, false, 0, true, ScheduledReportIntervalDays: 7, DistributionListConfigured: false));

        Assert.Contains(emailed, l => l.Text.Contains("emailed every 7 day(s)"));
        Assert.Contains(emailed, l => l.Text.Contains("2 saved Compliance snapshots include it"));
        Assert.Contains(off, l => l.Text.Contains("turned off"));
        Assert.Contains(noList, l => l.Text.Contains("no distribution list"));
        Assert.Contains(off, l => l.Text.Contains("No saved Compliance snapshot includes it"));
    }

    private static readonly DateTime Now = new(2026, 11, 11, 12, 0, 0, DateTimeKind.Utc);

    [Fact]
    public void PolicyEditImpact_ShowsReapplyDiffs_AndHealthUnderTheNewCadence()
    {
        var policy = new BackupPolicy { Id = 2, Name = "Gold", Version = 3, ExpectedBackupIntervalHours = 24, RetentionPolicyDays = 30 };
        var linked = new[]
        {
            // Last success 20 hours ago: OnTrack at 24 h, Overdue at 12 h.
            new PolicyEditImpactTarget(new PolicyTargetInstance(1, "ARX-01", 24, 30, null, 2, 3), Now.AddHours(-20), BackupHealth.OnTrack),
            // Failed stays Failed whatever the cadence.
            new PolicyEditImpactTarget(new PolicyTargetInstance(2, "ARX-02", 24, 30, null, 2, 3), Now.AddHours(-2), BackupHealth.Failed),
        };

        var impact = PolicyEditImpactBuilder.Build(policy, 12, 30, null, linked, Now);

        Assert.Empty(impact.Errors);
        Assert.Equal(new BulkEditFieldDiff("ExpectedBackupIntervalHours", "24", "12"), Assert.Single(impact.PolicyChanges));
        Assert.Equal(2, impact.LinkedCount);
        Assert.Equal(2, impact.WouldChangeCount);
        Assert.Equal(1, impact.BecomesOverdueCount);
        var first = impact.Rows[0];
        Assert.Equal("ARX-01", first.InstanceName);
        Assert.True(first.HealthChanges);
        Assert.Equal(BackupHealth.Failed, impact.Rows[1].HealthAfterReapply);
    }

    [Fact]
    public void PolicyEditImpact_RejectsInvalidValues()
    {
        var impact = PolicyEditImpactBuilder.Build(new BackupPolicy { ExpectedBackupIntervalHours = 24 }, 0, -1, 0, [], Now);

        Assert.Equal(3, impact.Errors.Count);
    }
}
