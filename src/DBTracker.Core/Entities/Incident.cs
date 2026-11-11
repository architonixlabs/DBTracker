using DBTracker.Core.Enums;

namespace DBTracker.Core.Entities;

/// <summary>The human response to an alert: assignment, acknowledgement, ticket reference, escalation. It never changes computed health.</summary>
public class Incident
{
    public int Id { get; set; }

    public int DatabaseInstanceId { get; set; }
    public DatabaseInstance? DatabaseInstance { get; set; }

    public BackupHealth TriggerHealth { get; set; }
    public DateTime OpenedAtUtc { get; set; } = DateTime.UtcNow;
    public DateTime? ClosedAtUtc { get; set; }

    public string AssigneeName { get; set; } = string.Empty;
    public string AcknowledgedBy { get; set; } = string.Empty;
    public DateTime? AcknowledgedAtUtc { get; set; }
    public string Note { get; set; } = string.Empty;
    public string TicketReference { get; set; } = string.Empty;

    /// <summary>Bumped by IncidentEscalationEvaluator while the incident stays open and unacknowledged past the configured reminder interval.</summary>
    public int EscalationLevel { get; set; }
    public DateTime? LastEscalatedAtUtc { get; set; }

    public bool IsOpen => ClosedAtUtc is null;
}
