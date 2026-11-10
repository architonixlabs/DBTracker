using DBTracker.Core.Entities;
using Microsoft.EntityFrameworkCore;

namespace DBTracker.Infrastructure.Notifications;

/// <summary>User-facing incident operations (U08) — acknowledge, assign, add a note/ticket reference. None of these touch backup health; they're a response-tracking overlay.</summary>
public class IncidentService(IDbContextFactory<DBTrackerDbContext> factory)
{
    public async Task<Incident?> GetOpenAsync(int instanceId)
    {
        using var db = await factory.CreateDbContextAsync();
        return await db.Incidents.AsNoTracking().FirstOrDefaultAsync(i => i.DatabaseInstanceId == instanceId && i.ClosedAtUtc == null);
    }

    public async Task<List<Incident>> ListOpenAsync()
    {
        using var db = await factory.CreateDbContextAsync();
        return await db.Incidents.AsNoTracking()
            .Include(i => i.DatabaseInstance)
            .Where(i => i.ClosedAtUtc == null)
            .OrderBy(i => i.OpenedAtUtc)
            .ToListAsync();
    }

    public async Task AcknowledgeAsync(int incidentId, string acknowledgedBy, string note, string ticketReference)
    {
        using var db = await factory.CreateDbContextAsync();
        var incident = await db.Incidents.FindAsync(incidentId);
        if (incident is null)
        {
            return;
        }

        incident.AcknowledgedBy = acknowledgedBy;
        incident.AcknowledgedAtUtc = DateTime.UtcNow;
        incident.Note = note;
        incident.TicketReference = ticketReference;
        await db.SaveChangesAsync();
    }

    public async Task AssignAsync(int incidentId, string assigneeName)
    {
        using var db = await factory.CreateDbContextAsync();
        var incident = await db.Incidents.FindAsync(incidentId);
        if (incident is null)
        {
            return;
        }

        incident.AssigneeName = assigneeName;
        await db.SaveChangesAsync();
    }
}
