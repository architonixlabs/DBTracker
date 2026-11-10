using DBTracker.Core.Entities;
using DBTracker.Core.Services;
using Xunit;

namespace DBTracker.UnitTests;

public class BulkEditPreviewBuilderTests
{
    private static DatabaseInstance Instance(int id, string name, string company = "OldCo") => new()
    {
        Id = id, Name = name, Company = company, Department = "Ops", ContactName = "Old Name", ContactEmail = "old@co.com",
        Tags = "old-tag", RetentionPolicyDays = 30
    };

    [Fact]
    public void NoFieldsSelected_ProducesNoRows()
    {
        var request = new BulkEditRequest(false, null, null, null, null, false, null, false, null);

        var rows = BulkEditPreviewBuilder.Preview([Instance(1, "A")], request);

        Assert.Empty(rows);
    }

    [Fact]
    public void OwnerChange_ProducesDiffForChangedFieldsOnly()
    {
        var request = new BulkEditRequest(true, "NewCo", "Ops", "Old Name", "old@co.com", false, null, false, null);

        var rows = BulkEditPreviewBuilder.Preview([Instance(1, "A")], request);

        var row = Assert.Single(rows);
        var change = Assert.Single(row.Changes);
        Assert.Equal("Company", change.Field);
        Assert.Equal("OldCo", change.Before);
        Assert.Equal("NewCo", change.After);
    }

    [Fact]
    public void IdenticalValues_ProduceNoDiffRow()
    {
        var request = new BulkEditRequest(true, "OldCo", "Ops", "Old Name", "old@co.com", false, null, false, null);

        var rows = BulkEditPreviewBuilder.Preview([Instance(1, "A")], request);

        Assert.Empty(rows);
    }

    [Fact]
    public void TagsAndRetention_BothChangeable()
    {
        var request = new BulkEditRequest(false, null, null, null, null, true, "new-tag", true, 90);

        var rows = BulkEditPreviewBuilder.Preview([Instance(1, "A")], request);

        var row = Assert.Single(rows);
        Assert.Equal(2, row.Changes.Count);
        Assert.Contains(row.Changes, c => c.Field == "Tags" && c.After == "new-tag");
        Assert.Contains(row.Changes, c => c.Field == "RetentionPolicyDays" && c.After == "90");
    }

    [Fact]
    public void MultipleInstances_OnlyChangedOnesAppearInPreview()
    {
        // Owner is one bundle of four fields (Company/Department/ContactName/ContactEmail) — the
        // request must match all four of instance 1's existing values exactly for it to produce no
        // diff; instance 2 differs only in Company and should be the only one previewed.
        var request = new BulkEditRequest(true, "NewCo", "Ops", "Old Name", "old@co.com", false, null, false, null);
        var instances = new[] { Instance(1, "A", "NewCo"), Instance(2, "B", "OldCo") };

        var rows = BulkEditPreviewBuilder.Preview(instances, request);

        var row = Assert.Single(rows);
        Assert.Equal(2, row.InstanceId);
    }

    [Fact]
    public void Apply_SetsOnlySelectedFields_LeavesOthersUntouched()
    {
        var instance = Instance(1, "A");
        var request = new BulkEditRequest(false, null, null, null, null, true, "replacement-tag", false, null);

        BulkEditPreviewBuilder.Apply(instance, request);

        Assert.Equal("replacement-tag", instance.Tags);
        Assert.Equal("OldCo", instance.Company); // untouched
        Assert.Equal(30, instance.RetentionPolicyDays); // untouched
    }

    [Fact]
    public void Request_HasNoArchiveField()
    {
        // BulkEditRequest must not be able to express archiving (U17) — enforced by the type
        // itself having no IsArchived/Archive property at all.
        var properties = typeof(BulkEditRequest).GetProperties().Select(p => p.Name);
        Assert.DoesNotContain(properties, n => n.Contains("Archiv", System.StringComparison.OrdinalIgnoreCase));
    }
}
