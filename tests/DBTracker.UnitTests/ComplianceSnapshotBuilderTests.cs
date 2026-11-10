using DBTracker.Core.Entities;
using DBTracker.Core.Enums;
using DBTracker.Core.Services;
using Xunit;

namespace DBTracker.UnitTests;

public class ComplianceSnapshotBuilderTests
{
    private static ComplianceReportRow Row(string name, BackupHealth health, bool restoreTested) =>
        new(new DatabaseInstance { Name = name, Company = "Acme", Department = "Ops", Role = InstanceRole.Active, Environment = EnvironmentTier.Production },
            health, DateTime.UtcNow.AddHours(-1), restoreTested);

    [Fact]
    public void BuildRows_SortsByInstanceName()
    {
        var rows = ComplianceSnapshotBuilder.BuildRows([
            Row("ZEBRA", BackupHealth.OnTrack, true),
            Row("ALPHA", BackupHealth.Failed, false)
        ]);

        Assert.Equal("ALPHA", rows[0].InstanceName);
        Assert.Equal("ZEBRA", rows[1].InstanceName);
    }

    [Fact]
    public void SerializeThenDeserialize_RoundTrips()
    {
        var rows = ComplianceSnapshotBuilder.BuildRows([Row("ACME-DB1", BackupHealth.Overdue, false)]);

        var json = ComplianceSnapshotBuilder.Serialize(rows);
        var roundTripped = ComplianceSnapshotBuilder.Deserialize(json);

        Assert.Single(roundTripped);
        Assert.Equal("ACME-DB1", roundTripped[0].InstanceName);
        Assert.Equal(BackupHealth.Overdue, roundTripped[0].Health);
        Assert.False(roundTripped[0].RestoreTested);
    }

    [Fact]
    public void Deserialize_EmptyString_ReturnsEmptyList()
    {
        Assert.Empty(ComplianceSnapshotBuilder.Deserialize(""));
    }

    [Fact]
    public void FrozenRows_AreUnaffectedByLaterChangesToTheSourceInstance()
    {
        var instance = new DatabaseInstance { Name = "MUTABLE-DB", Company = "Acme", Department = "Ops" };
        var sourceRow = new ComplianceReportRow(instance, BackupHealth.OnTrack, DateTime.UtcNow, true);

        var frozen = ComplianceSnapshotBuilder.BuildRows([sourceRow]);
        var json = ComplianceSnapshotBuilder.Serialize(frozen);

        // Simulate the instance being edited/archived after the snapshot was taken.
        instance.Company = "Globex";
        instance.Name = "RENAMED-DB";

        var reopened = ComplianceSnapshotBuilder.Deserialize(json);

        Assert.Equal("MUTABLE-DB", reopened[0].InstanceName);
        Assert.Equal("Acme", reopened[0].Company);
    }
}
