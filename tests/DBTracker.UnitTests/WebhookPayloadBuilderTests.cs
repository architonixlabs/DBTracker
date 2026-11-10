using DBTracker.Core.Entities;
using DBTracker.Core.Enums;
using DBTracker.Core.Services;
using Xunit;

namespace DBTracker.UnitTests;

public class WebhookPayloadBuilderTests
{
    private static readonly DateTime Now = new(2026, 11, 10, 9, 0, 0, DateTimeKind.Utc);

    private static DatabaseInstance Instance() => new()
    {
        Name = "CONTOSO-HR-MYSQL",
        Application = "Contoso HR",
        Company = "Contoso",
        Department = "Human Resources",
    };

    [Fact]
    public void Failed_ProducesHumanReadableText_AndStructuredFields()
    {
        var payload = WebhookPayloadBuilder.BuildAlertPayload(Instance(), BackupHealth.Failed, Now);

        Assert.Equal("backup_health_alert", payload.Event);
        Assert.Equal("CONTOSO-HR-MYSQL", payload.InstanceName);
        Assert.Equal("Failed", payload.Health);
        Assert.Equal(Now, payload.TimestampUtc);
        Assert.Contains("CONTOSO-HR-MYSQL", payload.Text);
        Assert.Contains("failing backups", payload.Text);
        Assert.Contains("Contoso", payload.Text);
    }

    [Fact]
    public void Overdue_UsesOverdueWording()
    {
        var payload = WebhookPayloadBuilder.BuildAlertPayload(Instance(), BackupHealth.Overdue, Now);

        Assert.Contains("overdue for backup", payload.Text);
    }
}
