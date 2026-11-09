using DBTracker.Core.Enums;

namespace DBTracker.Core.Entities;

/// <summary>A restore performed from a backup — DR drill, incident, data request or scheduled test. Evidence that a backup is actually recoverable, not just taken.</summary>
public class RestoreEvent
{
    public int Id { get; set; }

    public int DatabaseInstanceId { get; set; }
    public DatabaseInstance? DatabaseInstance { get; set; }

    public DateTime TimestampUtc { get; set; } = DateTime.UtcNow;
    public string PerformedBy { get; set; } = string.Empty;
    public RestoreReason Reason { get; set; } = RestoreReason.Test;
    public RestoreOutcome Outcome { get; set; } = RestoreOutcome.Success;
    public string Notes { get; set; } = string.Empty;

    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;
}
