using DBTracker.Core.Enums;

namespace DBTracker.Core.Entities;

/// <summary>One renewal or key rotation recorded against a certificate dependency. Append-only, so the renewal history survives later edits.
/// Records what a person says was done elsewhere; DBTracker never issues, renews or rotates anything itself.</summary>
public class CertificateRenewalEvent
{
    public int Id { get; set; }

    public int CertificateDependencyId { get; set; }
    public CertificateDependency? CertificateDependency { get; set; }

    public CertificateEventType EventType { get; set; }

    public DateTime OccurredAtUtc { get; set; }
    public DateTime? PreviousExpiresAtUtc { get; set; }
    public DateTime? NewExpiresAtUtc { get; set; }

    /// <summary>Ticket, change or secret-store version reference. Screened by SecretLeakGuard before saving.</summary>
    public string Evidence { get; set; } = string.Empty;

    /// <summary>Which replicas or clients were updated in step with this renewal or rotation.</summary>
    public string ReplicasOrClientsUpdated { get; set; } = string.Empty;

    public DateTime RecordedAtUtc { get; set; } = DateTime.UtcNow;
    public string RecordedBy { get; set; } = string.Empty;
}
