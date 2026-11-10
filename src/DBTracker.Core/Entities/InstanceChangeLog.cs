namespace DBTracker.Core.Entities;

/// <summary>A single field-level change to a DatabaseInstance record — audit evidence of who changed what, when, not just that the record was touched.</summary>
public class InstanceChangeLog
{
    public int Id { get; set; }

    public int DatabaseInstanceId { get; set; }
    public DatabaseInstance? DatabaseInstance { get; set; }

    public DateTime ChangedAtUtc { get; set; } = DateTime.UtcNow;
    public string ChangedBy { get; set; } = string.Empty;
    public string FieldName { get; set; } = string.Empty;
    public string OldValue { get; set; } = string.Empty;
    public string NewValue { get; set; } = string.Empty;
}
