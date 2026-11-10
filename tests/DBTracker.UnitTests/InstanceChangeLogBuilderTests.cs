using DBTracker.Core.Entities;
using DBTracker.Core.Enums;
using DBTracker.Core.Services;
using Xunit;

namespace DBTracker.UnitTests;

public class InstanceChangeLogBuilderTests
{
    private static readonly DateTime Now = new(2026, 11, 10, 9, 0, 0, DateTimeKind.Utc);

    private static DatabaseInstance Instance() => new()
    {
        Id = 7,
        Name = "ORIG-NAME",
        Engine = DatabaseEngine.PostgreSql,
        ContactEmail = "a@example.com",
        ExpectedBackupIntervalHours = 24,
    };

    [Fact]
    public void NoChanges_ProducesNoEntries()
    {
        var instance = Instance();
        var entries = InstanceChangeLogBuilder.Diff(instance, instance, "priya@architonixlabs.com", Now);

        Assert.Empty(entries);
    }

    [Fact]
    public void ChangedField_ProducesOneEntry_WithOldAndNewValue()
    {
        var before = Instance();
        var after = Instance();
        after.Name = "NEW-NAME";

        var entries = InstanceChangeLogBuilder.Diff(before, after, "priya@architonixlabs.com", Now);

        var entry = Assert.Single(entries);
        Assert.Equal("Name", entry.FieldName);
        Assert.Equal("ORIG-NAME", entry.OldValue);
        Assert.Equal("NEW-NAME", entry.NewValue);
        Assert.Equal("priya@architonixlabs.com", entry.ChangedBy);
        Assert.Equal(Now, entry.ChangedAtUtc);
        Assert.Equal(7, entry.DatabaseInstanceId);
    }

    [Fact]
    public void MultipleChangedFields_ProduceOneEntryEach()
    {
        var before = Instance();
        var after = Instance();
        after.ContactEmail = "b@example.com";
        after.ExpectedBackupIntervalHours = 48;

        var entries = InstanceChangeLogBuilder.Diff(before, after, "admin@architonixlabs.com", Now);

        Assert.Equal(2, entries.Count);
        Assert.Contains(entries, e => e.FieldName == "ContactEmail" && e.NewValue == "b@example.com");
        Assert.Contains(entries, e => e.FieldName == "ExpectedBackupIntervalHours" && e.OldValue == "24" && e.NewValue == "48");
    }

    [Fact]
    public void EnumFieldChange_IsDiffed_ByName()
    {
        var before = Instance();
        var after = Instance();
        after.Engine = DatabaseEngine.MySql;

        var entry = Assert.Single(InstanceChangeLogBuilder.Diff(before, after, "x", Now));

        Assert.Equal("Engine", entry.FieldName);
        Assert.Equal("PostgreSql", entry.OldValue);
        Assert.Equal("MySql", entry.NewValue);
    }

    [Fact]
    public void NullableRetentionPolicy_SetFromNull_IsDiffed()
    {
        var before = Instance();
        var after = Instance();
        after.RetentionPolicyDays = 30;

        var entry = Assert.Single(InstanceChangeLogBuilder.Diff(before, after, "x", Now));

        Assert.Equal("RetentionPolicyDays", entry.FieldName);
        Assert.Equal("", entry.OldValue);
        Assert.Equal("30", entry.NewValue);
    }
}
