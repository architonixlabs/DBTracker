using System.Text.Json;
using DBTracker.Core.Entities;
using DBTracker.Infrastructure.Audit;
using Microsoft.EntityFrameworkCore;

namespace DBTracker.Infrastructure.Notifications;

/// <summary>Acknowledge, assign, and note incidents. None of these touch backup health. Each action is written to the audit trail (U19).</summary>
public class IncidentService(IDbContextFactory<DBTrackerDbContext> factory, AuditRecorder audit)
{
    public const string AuditTargetType = "Incident";

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

    /// <summary>Incidents open at a past instant, from their stored OpenedAtUtc/ClosedAtUtc rather than recomputed health.</summary>
    public async Task<List<Incident>> ListOpenAsOfAsync(DateTime asOfUtc)
    {
        using var db = await factory.CreateDbContextAsync();
        return await db.Incidents.AsNoTracking()
            .Include(i => i.DatabaseInstance)
            .Where(i => i.OpenedAtUtc <= asOfUtc && (i.ClosedAtUtc == null || i.ClosedAtUtc > asOfUtc))
            .OrderBy(i => i.OpenedAtUtc)
            .ToListAsync();
    }

    /// <summary>Records who acknowledged, with note and ticket. Returns false when the incident is gone or already closed.</summary>
    public async Task<bool> AcknowledgeAsync(int incidentId, string actorUserId, string actorName, string note, string ticketReference)
    {
        using var db = await factory.CreateDbContextAsync();
        var incident = await db.Incidents.Include(i => i.DatabaseInstance).FirstOrDefaultAsync(i => i.Id == incidentId);
        if (incident is null || incident.ClosedAtUtc is not null)
        {
            return false;
        }

        var before = Snapshot(incident);
        incident.AcknowledgedBy = actorName;
        incident.AcknowledgedAtUtc = DateTime.UtcNow;
        incident.Note = (note ?? "").Trim();
        incident.TicketReference = (ticketReference ?? "").Trim();
        await db.SaveChangesAsync();

        await audit.RecordUserActionAsync(actorUserId, actorName, "IncidentAcknowledged", AuditTargetType, incident.Id.ToString(),
            incident.DatabaseInstance?.Name ?? $"Instance #{incident.DatabaseInstanceId}",
            reason: "Acknowledgement is a response record only; backup health is unchanged.",
            beforeJson: before, afterJson: Snapshot(incident));
        return true;
    }

    /// <summary>Sets or clears the assignee. Returns false when the incident is gone, closed, or the assignee is unchanged.</summary>
    public async Task<bool> AssignAsync(int incidentId, string assigneeName, string actorUserId, string actorName)
    {
        using var db = await factory.CreateDbContextAsync();
        var incident = await db.Incidents.Include(i => i.DatabaseInstance).FirstOrDefaultAsync(i => i.Id == incidentId);
        var newAssignee = (assigneeName ?? "").Trim();
        if (incident is null || incident.ClosedAtUtc is not null || incident.AssigneeName == newAssignee)
        {
            return false;
        }

        var before = Snapshot(incident);
        incident.AssigneeName = newAssignee;
        await db.SaveChangesAsync();

        await audit.RecordUserActionAsync(actorUserId, actorName, newAssignee.Length == 0 ? "IncidentUnassigned" : "IncidentAssigned",
            AuditTargetType, incident.Id.ToString(),
            incident.DatabaseInstance?.Name ?? $"Instance #{incident.DatabaseInstanceId}",
            beforeJson: before, afterJson: Snapshot(incident));
        return true;
    }

    private static string Snapshot(Incident i) => JsonSerializer.Serialize(new
    {
        i.AssigneeName,
        i.AcknowledgedBy,
        i.AcknowledgedAtUtc,
        i.Note,
        i.TicketReference,
        i.EscalationLevel
    });
}
