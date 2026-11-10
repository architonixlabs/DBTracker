using DBTracker.Core.Entities;
using DBTracker.Core.Services;
using Xunit;

namespace DBTracker.UnitTests;

public class BackupPolicyApplyPreviewBuilderTests
{
    private static BackupPolicy Policy(int id = 1, int version = 1, int hours = 24, int? retention = 30, int? restoreTest = 90) =>
        new() { Id = id, Version = version, Name = "Standard", ExpectedBackupIntervalHours = hours, RetentionPolicyDays = retention, RestoreTestIntervalDays = restoreTest };

    [Fact]
    public void Preview_ShowsFieldDiffsForEachChangedValue()
    {
        var instances = new[] { new PolicyTargetInstance(1, "A", 48, null, null, null, null) };
        var rows = BackupPolicyApplyPreviewBuilder.Preview(instances, Policy());

        var row = Assert.Single(rows);
        Assert.Equal(3, row.Changes.Count);
        Assert.Contains(row.Changes, c => c.Field == "ExpectedBackupIntervalHours" && c.Before == "48" && c.After == "24");
        Assert.Contains(row.Changes, c => c.Field == "RetentionPolicyDays" && c.Before == "" && c.After == "30");
        Assert.False(row.AlreadyUpToDate);
    }

    [Fact]
    public void Preview_InstanceAlreadyMatchingPolicyValues_NoDiffs()
    {
        var instances = new[] { new PolicyTargetInstance(1, "A", 24, 30, 90, null, null) };
        var rows = BackupPolicyApplyPreviewBuilder.Preview(instances, Policy());

        var row = Assert.Single(rows);
        Assert.Empty(row.Changes);
    }

    [Fact]
    public void Preview_AlreadyAppliedSameVersionWithNoDrift_IsUpToDate()
    {
        var instances = new[] { new PolicyTargetInstance(1, "A", 24, 30, 90, AppliedPolicyId: 1, AppliedPolicyVersion: 2) };
        var rows = BackupPolicyApplyPreviewBuilder.Preview(instances, Policy(id: 1, version: 2));

        Assert.True(rows.Single().AlreadyUpToDate);
    }

    [Fact]
    public void Preview_AppliedButPolicyVersionBumpedSince_NotUpToDateEvenWithoutFieldDrift()
    {
        // Applied v1 values happened to already equal v2's values, but the instance's recorded
        // AppliedPolicyVersion is stale — still flagged, since the policy itself has moved on.
        var instances = new[] { new PolicyTargetInstance(1, "A", 24, 30, 90, AppliedPolicyId: 1, AppliedPolicyVersion: 1) };
        var rows = BackupPolicyApplyPreviewBuilder.Preview(instances, Policy(id: 1, version: 2));

        Assert.False(rows.Single().AlreadyUpToDate);
    }

    [Fact]
    public void Apply_SetsInstanceFieldsAndPolicyProvenance()
    {
        var instance = new DatabaseInstance { Id = 1, Name = "A", ExpectedBackupIntervalHours = 48 };
        var policy = Policy(id: 7, version: 3, hours: 12, retention: 60, restoreTest: 45);

        BackupPolicyApplyPreviewBuilder.Apply(instance, policy);

        Assert.Equal(12, instance.ExpectedBackupIntervalHours);
        Assert.Equal(60, instance.RetentionPolicyDays);
        Assert.Equal(45, instance.RestoreTestIntervalDaysOverride);
        Assert.Equal(7, instance.AppliedPolicyId);
        Assert.Equal(3, instance.AppliedPolicyVersion);
    }
}
