using DBTracker.Core.Enums;
using DBTracker.Core.Services;
using Xunit;

namespace DBTracker.UnitTests;

public class InstanceListFilterTests
{
    private static InstanceListRow Row(
        int id, string name, DatabaseEngine engine = DatabaseEngine.SqlServer, string company = "Acme",
        string department = "Ops", string owner = "Priya", string tags = "", BackupHealth health = BackupHealth.OnTrack,
        InstanceRole role = InstanceRole.Active, EnvironmentTier env = EnvironmentTier.Production) =>
        new(id, name, engine, "1.0", "App", company, department, tags, owner, $"{owner.ToLower()}@acme.test",
            role, env, "10.0.0.1", health, null, null, "", null, ScanStatus.NeverScanned, "");

    [Fact]
    public void Search_MatchesNameCaseInsensitive()
    {
        var rows = new[] { Row(1, "PROD-SQL-01"), Row(2, "OtherDb") };
        var result = InstanceListFilter.Apply(rows, new InstanceListQuery(Search: "prod-sql"));
        Assert.Single(result.Items);
        Assert.Equal("PROD-SQL-01", result.Items[0].Name);
    }

    [Fact]
    public void HealthFilter_OnlyMatchingHealthReturned()
    {
        var rows = new[] { Row(1, "A", health: BackupHealth.Failed), Row(2, "B", health: BackupHealth.OnTrack) };
        var result = InstanceListFilter.Apply(rows, new InstanceListQuery(Health: BackupHealth.Failed));
        Assert.Single(result.Items);
        Assert.Equal("A", result.Items[0].Name);
    }

    [Fact]
    public void TagFilter_MatchesOneOfSeveralCommaSeparatedTags()
    {
        var rows = new[] { Row(1, "A", tags: "finance, pci"), Row(2, "B", tags: "internal") };
        var result = InstanceListFilter.Apply(rows, new InstanceListQuery(Tag: "pci"));
        Assert.Single(result.Items);
        Assert.Equal("A", result.Items[0].Name);
    }

    [Fact]
    public void OwnerFilter_MatchesContactName()
    {
        var rows = new[] { Row(1, "A", owner: "Priya"), Row(2, "B", owner: "Marcus") };
        var result = InstanceListFilter.Apply(rows, new InstanceListQuery(Owner: "Marcus"));
        Assert.Single(result.Items);
        Assert.Equal("B", result.Items[0].Name);
    }

    [Fact]
    public void MyInstancesOnly_RequiresEmailMatch()
    {
        var rows = new[] { Row(1, "Mine", owner: "Dev"), Row(2, "NotMine", owner: "Priya") };
        var result = InstanceListFilter.Apply(rows, new InstanceListQuery(MyInstancesOnly: true, MyEmail: "dev@acme.test"));
        Assert.Single(result.Items);
        Assert.Equal("Mine", result.Items[0].Name);
    }

    [Fact]
    public void RiskSort_FailedBeforeOverdueBeforeOnTrack()
    {
        var rows = new[]
        {
            Row(1, "OK", health: BackupHealth.OnTrack),
            Row(2, "Bad", health: BackupHealth.Failed),
            Row(3, "Late", health: BackupHealth.Overdue),
        };
        var result = InstanceListFilter.Apply(rows, new InstanceListQuery(Sort: InstanceSortKey.Risk));
        Assert.Equal(new[] { "Bad", "Late", "OK" }, result.Items.Select(r => r.Name));
    }

    [Fact]
    public void Sort_Descending_ReversesOrder()
    {
        var rows = new[] { Row(1, "A"), Row(2, "B"), Row(3, "C") };
        var result = InstanceListFilter.Apply(rows, new InstanceListQuery(Sort: InstanceSortKey.Name, Descending: true));
        Assert.Equal(new[] { "C", "B", "A" }, result.Items.Select(r => r.Name));
    }

    [Fact]
    public void Paging_ReturnsCorrectSliceAndTotalCount()
    {
        var rows = Enumerable.Range(1, 30).Select(i => Row(i, $"I{i:D2}")).ToArray();
        var page2 = InstanceListFilter.Apply(rows, new InstanceListQuery(Page: 2, PageSize: 10));

        Assert.Equal(30, page2.TotalCount);
        Assert.Equal(3, page2.TotalPages);
        Assert.Equal(10, page2.Items.Count);
        Assert.Equal("I11", page2.Items[0].Name);
    }

    [Fact]
    public void Paging_PageBeyondLastPage_ClampsToLastPage()
    {
        var rows = Enumerable.Range(1, 5).Select(i => Row(i, $"I{i}")).ToArray();
        var result = InstanceListFilter.Apply(rows, new InstanceListQuery(Page: 99, PageSize: 10));

        Assert.Equal(1, result.Page);
        Assert.Equal(5, result.Items.Count);
    }

    [Fact]
    public void PageSize_ClampedToMax()
    {
        var rows = Enumerable.Range(1, 5).Select(i => Row(i, $"I{i}")).ToArray();
        var result = InstanceListFilter.Apply(rows, new InstanceListQuery(PageSize: 100_000));
        Assert.Equal(InstanceListFilter.MaxPageSize, result.PageSize);
    }

    [Fact]
    public void SplitTags_TrimsAndDropsEmptyEntries()
    {
        var tags = InstanceListFilter.SplitTags("finance, , pci ;internal");
        Assert.Equal(new[] { "finance", "pci", "internal" }, tags);
    }
}
