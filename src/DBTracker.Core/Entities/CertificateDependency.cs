using DBTracker.Core.Enums;

namespace DBTracker.Core.Entities;

/// <summary>
/// D20 — a TLS certificate or recovery-key/encryption-certificate dependency for one instance.
/// SecretStoreReference is a pointer into an approved secret-management system (e.g. a Vault path or
/// key-vault secret name) and must never hold the actual secret/private key — there is deliberately
/// no field on this entity that could hold one, and SecretLeakGuard additionally screens free-text
/// fields (Notes, RenewalEvidenceText) before they're ever shown or exported. VerifiedBackupRestoreAccess/
/// LastAccessCheckAtUtc record that someone proved the key can actually decrypt a real backup/restore
/// without storing any private material to do it. LastRotatedAtUtc/RotationIntervalDays are a wholly
/// separate concept from ExpiresAtUtc — a certificate's expiry and its key's rotation cadence are
/// evaluated independently (CertificateExpiryEvaluator vs. KeyRotationEvaluator).
/// </summary>
public class CertificateDependency
{
    public int Id { get; set; }

    public int DatabaseInstanceId { get; set; }
    public DatabaseInstance? DatabaseInstance { get; set; }

    public CertificateKind Kind { get; set; }
    public string Label { get; set; } = string.Empty;

    public int? OwnerTeamId { get; set; }
    public Team? OwnerTeam { get; set; }
    public string OwnerName { get; set; } = string.Empty;

    public DateTime? ExpiresAtUtc { get; set; }

    /// <summary>Reference into an approved secret-management system — never the secret/private key itself.</summary>
    public string SecretStoreReference { get; set; } = string.Empty;

    public bool VerifiedBackupRestoreAccess { get; set; }
    public DateTime? LastAccessCheckAtUtc { get; set; }
    public string LastAccessCheckBy { get; set; } = string.Empty;

    /// <summary>Key rotation (distinct from certificate expiry) — when the underlying key material was last rotated, and how often it should be.</summary>
    public DateTime? LastRotatedAtUtc { get; set; }
    public int? RotationIntervalDays { get; set; }

    /// <summary>Which replicas/clients also depend on this certificate/key and must be renewed in step — free text, since the shape varies too much per engine/topology to model structurally here.</summary>
    public string AffectedReplicasOrClientsText { get; set; } = string.Empty;

    public DateTime? LastRenewedAtUtc { get; set; }
    public string RenewalEvidenceText { get; set; } = string.Empty;

    public string Notes { get; set; } = string.Empty;

    /// <summary>Dedupe state for the expiry-notice sweep (CertificateExpiryService), same pattern as D03's SupportRunwayService/VersionIdentity.LastRunwayNoticeThresholdDays — reuses SupportRunwayNoticeEvaluator's generic threshold logic rather than a parallel implementation.</summary>
    public int? LastExpiryNoticeThresholdDays { get; set; }
    public DateTime? LastExpiryNoticeAtUtc { get; set; }

    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAtUtc { get; set; } = DateTime.UtcNow;
    public string UpdatedBy { get; set; } = string.Empty;
}
