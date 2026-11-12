using DBTracker.Core.Entities;
using DBTracker.Core.Enums;
using Microsoft.EntityFrameworkCore;

namespace DBTracker.Infrastructure.Notifications;

/// <summary>Opens and closes incidents on the alert sweep's own DbContext, so they save in the same SaveChangesAsync as LastNotifiedHealth.</summary>
public static class IncidentLifecycle
{
    /// <summary>Returns the open incident for this instance or opens one; never two at once.</summary>
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

    /// <summary>Closes any open incident for this instance (health recovered). Returns the incidents it closed, empty when none was open.</summary>
    public static async Task<List<Incident>> CloseOpenAsync(DBTrackerDbContext db, int instanceId, DateTime nowUtc)
    {
        var open = await db.Incidents.Where(i => i.DatabaseInstanceId == instanceId && i.ClosedAtUtc == null).ToListAsync();
        foreach (var incident in open)
        {
            incident.ClosedAtUtc = nowUtc;
        }

        return open;
    }
}
