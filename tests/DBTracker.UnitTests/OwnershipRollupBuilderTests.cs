using DBTracker.Core.Enums;
using DBTracker.Core.Services;
using Xunit;

namespace DBTracker.UnitTests;

public class OwnershipRollupBuilderTests
{
    [Fact]
    public void GroupsByCompanyAndDepartment()
    {
        var inputs = new[]
        {
            new OwnershipRollupInput("Contoso", "Sales", BackupHealth.OnTrack, "Marcus Chen"),
            new OwnershipRollupInput("Contoso", "Sales", BackupHealth.Failed, "Marcus Chen"),
            new OwnershipRollupInput("Contoso", "HR", BackupHealth.OnTrack, "Angela Price"),
        };

        var rows = OwnershipRollupBuilder.Build(inputs);

        Assert.Equal(2, rows.Count);
        var sales = Assert.Single(rows, r => r.Department == "Sales");
        Assert.Equal(2, sales.InstanceCount);
        Assert.Equal(1, sales.OnTrack);
        Assert.Equal(1, sales.Failed);
    }

    [Fact]
    public void PrimaryContacts_DeduplicatedAndBlanksExcluded()
    {
        var inputs = new[]
        {
            new OwnershipRollupInput("Acme", "Ops", BackupHealth.OnTrack, "Priya Nair"),
            new OwnershipRollupInput("Acme", "Ops", BackupHealth.OnTrack, "Priya Nair"),
            new OwnershipRollupInput("Acme", "Ops", BackupHealth.OnTrack, ""),
        };

        var row = Assert.Single(OwnershipRollupBuilder.Build(inputs));

        Assert.Equal("Priya Nair", row.PrimaryContacts);
        Assert.Equal(3, row.InstanceCount);
    }

    [Fact]
    public void OrderedByCompanyThenDepartment()
    {
        var inputs = new[]
        {
            new OwnershipRollupInput("Zeta", "Ops", BackupHealth.OnTrack, "X"),
            new OwnershipRollupInput("Acme", "Sales", BackupHealth.OnTrack, "Y"),
            new OwnershipRollupInput("Acme", "HR", BackupHealth.OnTrack, "Z"),
        };

        var rows = OwnershipRollupBuilder.Build(inputs);

        Assert.Equal(["Acme", "Acme", "Zeta"], rows.Select(r => r.Company).ToArray());
        Assert.Equal("HR", rows[0].Department);
        Assert.Equal("Sales", rows[1].Department);
    }
}
