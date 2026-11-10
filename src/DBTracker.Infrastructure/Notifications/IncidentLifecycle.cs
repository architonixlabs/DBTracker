using DBTracker.Core.Entities;
using DBTracker.Core.Enums;
using Microsoft.EntityFrameworkCore;

namespace DBTracker.Infrastructure.Notifications;

/// <summary>
/// Open/close glue used by AlertCheckHostedService on its own scoped DbContext, so opening,
/// closing and the LastNotifiedHealth update it sits alongside all land in one SaveChangesAsync.
/// The actual decisions (when to escalate) live in the pure, unit-tested IncidentEscalationEvaluator;
/// this is just the "is there already an open one" plumbing around it.
/// </summary>
public static class IncidentLifecycle
{
    /// <summary>Returns the existing open incident for this instance, or opens a new one — never two open at once for the same instance.</summary>
    public static async Task<Incident> OpenOrContinueAsync(DBTrackerDbContext db, int instanceId, BackupHealth triggerHealth, DateTime nowUtc)
    {
        var open = await db.Incidents.FirstOrDefaultAsync(i => i.DatabaseInstanceId == instanceId && i.ClosedAtUtc == null);
        if (open is not null)
        {
            return open;
        }

        var incident = new Incident { DatabaseInstanceId = instanceId, TriggerHealth = triggerHealth, OpenedAtUtc = nowUtc };
        db.Incidents.Add(incident);
        return incident;
    }

    /// <summary>Closes any open incident for this instance (health recovered). Returns true if one was actually open.</summary>
    public static async Task<bool> CloseOpenAsync(DBTrackerDbContext db, int instanceId, DateTime nowUtc)
    {
        var open = await db.Incidents.Where(i => i.DatabaseInstanceId == instanceId && i.ClosedAtUtc == null).ToListAsync();
        foreach (var incident in open)
        {
            incident.ClosedAtUtc = nowUtc;
        }

        return open.Count > 0;
    }
}
