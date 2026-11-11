namespace DBTracker.Core.Entities;

/// <summary>Links a contract to a covered instance. An instance can carry several contracts; the most favorable current status wins.</summary>
public class SupportContractAssignment
{
    public int Id { get; set; }

    public int SupportContractId { get; set; }
    public SupportContract? SupportContract { get; set; }

    public int DatabaseInstanceId { get; set; }
    public DatabaseInstance? DatabaseInstance { get; set; }

    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;
    public string CreatedBy { get; set; } = string.Empty;
}
