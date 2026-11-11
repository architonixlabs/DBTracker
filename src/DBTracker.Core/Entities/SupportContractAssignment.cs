namespace DBTracker.Core.Entities;

/// <summary>
/// D21 — join row linking one SupportContract to one covered DatabaseInstance. A contract can cover
/// several instances (one row each) and an instance can carry more than one contract (e.g. a prior
/// expired agreement kept for history plus a current renewal) — EntitlementStatusClassifier looks at
/// every assignment for the instance and reports the most favorable current status.
/// </summary>
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
