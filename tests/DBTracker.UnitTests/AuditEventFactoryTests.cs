using DBTracker.Core.Enums;
using DBTracker.Core.Services;
using Xunit;

namespace DBTracker.UnitTests;

public class AuditEventFactoryTests
{
    private static readonly DateTime Now = new(2026, 11, 10, 12, 0, 0, DateTimeKind.Utc);

    [Fact]
    public void ForUser_RecordsActorIdAndType()
    {
        var evt = AuditEventFactory.ForUser("user-1", "priya.nair@architonixlabs.com", "InstanceArchived", "DatabaseInstance", "42", "ARXMAIL-PROD-01", Now, reason: "Decommissioned");

        Assert.Equal(AuditActorType.User, evt.ActorType);
        Assert.Equal("user-1", evt.ActorUserId);
        Assert.Equal("priya.nair@architonixlabs.com", evt.ActorName);
        Assert.Equal("Decommissioned", evt.Reason);
    }

    [Fact]
    public void ForSystem_HasNoActorUserId_AndPrefixesName()
    {
        var evt = AuditEventFactory.ForSystem("AlertCheckHostedService", "IncidentOpened", "Incident", "7", "ARXMAIL-PROD-01", Now);

        Assert.Equal(AuditActorType.System, evt.ActorType);
        Assert.Equal("", evt.ActorUserId);
        Assert.StartsWith(AuditEventFactory.SystemActorPrefix, evt.ActorName);
        Assert.Contains("AlertCheckHostedService", evt.ActorName);
    }

    [Fact]
    public void SystemActorName_IsNotDoublePrefixed_IfAlreadyPrefixed()
    {
        var evt = AuditEventFactory.Create(AuditActorType.System, "", AuditEventFactory.SystemActorPrefix + "ReportHostedService",
            "ReportSent", "Report", "", "", Now);

        Assert.Equal(1, CountOccurrences(evt.ActorName, AuditEventFactory.SystemActorPrefix));
    }

    [Fact]
    public void MissingAction_Throws()
    {
        Assert.Throws<ArgumentException>(() =>
            AuditEventFactory.Create(AuditActorType.User, "u1", "someone", "", "DatabaseInstance", "1", "X", Now));
    }

    [Fact]
    public void BeforeAfterJson_AreCarriedThrough()
    {
        var evt = AuditEventFactory.ForUser("u1", "someone", "InstanceBulkEdited", "DatabaseInstance", "1", "X", Now,
            beforeJson: "{\"Tags\":\"a\"}", afterJson: "{\"Tags\":\"b\"}");

        Assert.Equal("{\"Tags\":\"a\"}", evt.BeforeJson);
        Assert.Equal("{\"Tags\":\"b\"}", evt.AfterJson);
    }

    private static int CountOccurrences(string haystack, string needle)
    {
        var count = 0;
        var index = 0;
        while ((index = haystack.IndexOf(needle, index, StringComparison.Ordinal)) >= 0)
        {
            count++;
            index += needle.Length;
        }
        return count;
    }
}
