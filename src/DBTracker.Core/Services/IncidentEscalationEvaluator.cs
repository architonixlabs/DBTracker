using DBTracker.Core.Entities;

namespace DBTracker.Core.Services;

/// <summary>
/// Whether an open incident (U08) is due for another reminder/escalation. Acknowledging an
/// incident stops escalation outright — Priya/Noor's whole point in acknowledging is to avoid a
/// duplicate response, so a recognized incident must stop paging people.
/// </summary>
public static class IncidentEscalationEvaluator
{
    public static bool ShouldEscalate(Incident incident, DateTime nowUtc, int escalationIntervalMinutes)
    {
        if (!incident.IsOpen || incident.AcknowledgedAtUtc is not null || escalationIntervalMinutes <= 0)
        {
            return false;
        }

        var since = incident.LastEscalatedAtUtc ?? incident.OpenedAtUtc;
        return nowUtc >= since.AddMinutes(escalationIntervalMinutes);
    }
}
