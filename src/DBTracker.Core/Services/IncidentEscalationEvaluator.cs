using DBTracker.Core.Entities;

namespace DBTracker.Core.Services;

/// <summary>Whether an open incident is due for another reminder. Acknowledging stops escalation, since its purpose is to prevent a duplicate response.</summary>
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
