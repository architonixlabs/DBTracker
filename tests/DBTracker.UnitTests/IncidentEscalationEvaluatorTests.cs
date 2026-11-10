using DBTracker.Core.Entities;
using DBTracker.Core.Enums;
using DBTracker.Core.Services;
using Xunit;

namespace DBTracker.UnitTests;

public class IncidentEscalationEvaluatorTests
{
    private static readonly DateTime OpenedAt = new(2026, 11, 10, 9, 0, 0, DateTimeKind.Utc);

    private static Incident OpenIncident() => new() { OpenedAtUtc = OpenedAt, TriggerHealth = BackupHealth.Failed };

    [Fact]
    public void ClosedIncident_NeverEscalates()
    {
        var incident = OpenIncident();
        incident.ClosedAtUtc = OpenedAt.AddMinutes(10);
        Assert.False(IncidentEscalationEvaluator.ShouldEscalate(incident, OpenedAt.AddHours(10), escalationIntervalMinutes: 60));
    }

    [Fact]
    public void AcknowledgedIncident_NeverEscalates()
    {
        var incident = OpenIncident();
        incident.AcknowledgedAtUtc = OpenedAt.AddMinutes(5);
        Assert.False(IncidentEscalationEvaluator.ShouldEscalate(incident, OpenedAt.AddHours(10), escalationIntervalMinutes: 60));
    }

    [Fact]
    public void DisabledInterval_NeverEscalates()
    {
        var incident = OpenIncident();
        Assert.False(IncidentEscalationEvaluator.ShouldEscalate(incident, OpenedAt.AddDays(1), escalationIntervalMinutes: 0));
    }

    [Fact]
    public void BeforeIntervalElapsed_DoesNotEscalateYet()
    {
        var incident = OpenIncident();
        Assert.False(IncidentEscalationEvaluator.ShouldEscalate(incident, OpenedAt.AddMinutes(59), escalationIntervalMinutes: 60));
    }

    [Fact]
    public void AfterIntervalElapsed_Escalates()
    {
        var incident = OpenIncident();
        Assert.True(IncidentEscalationEvaluator.ShouldEscalate(incident, OpenedAt.AddMinutes(61), escalationIntervalMinutes: 60));
    }

    [Fact]
    public void SubsequentEscalation_MeasuredFromLastEscalation_NotOriginalOpen()
    {
        var incident = OpenIncident();
        incident.LastEscalatedAtUtc = OpenedAt.AddMinutes(60);

        // Only 30 min since the last escalation, even though it's 90 min since the incident opened.
        Assert.False(IncidentEscalationEvaluator.ShouldEscalate(incident, OpenedAt.AddMinutes(90), escalationIntervalMinutes: 60));
        Assert.True(IncidentEscalationEvaluator.ShouldEscalate(incident, OpenedAt.AddMinutes(121), escalationIntervalMinutes: 60));
    }
}
