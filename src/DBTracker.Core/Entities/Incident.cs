using DBTracker.Core.Enums;

namespace DBTracker.Core.Entities;

/// <summary>
/// Tracks the human response to an instance's alertable health (U08) — assignment, acknowledgement,
/// a note/runbook/ticket reference, and escalation — entirely separate from BackupHealthCalculator's
/// output. Acknowledging or assigning an incident never changes the computed health; it only records
/// that someone is on it.
/// </summary>
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
