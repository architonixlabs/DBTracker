using DBTracker.Core.Enums;

namespace DBTracker.Core.Entities;

/// <summary>A storage/copy location a recovery depends on. FirstDiscoveredAtUtc (seen once) and VerificationStatus (checked now) are separate claims, so an unchecked copy never looks freshly verified.</summary>
/// <remarks>StoragePathOrReference is masked by role on every view and export; only Admin and DBA see the raw path.</remarks>
public class BackupCopyLocation
{
    public int Id { get; set; }

    public int DatabaseInstanceId { get; set; }
    public DatabaseInstance? DatabaseInstance { get; set; }

    public string Label { get; set; } = string.Empty;
    public bool IsOffsite { get; set; }

    /// <summary>Flagged by whoever recorded this location — a claim, not a verified guarantee of provider-side immutability.</summary>
    public bool IsImmutableFlagged { get; set; }
    public bool EncryptionExpected { get; set; }
    /// <summary>What the last check found about encryption at rest for this copy. Null means not checked, which never counts as encrypted.</summary>
    public bool? EncryptionConfirmed { get; set; }

    /// <summary>Raw storage path/URL/bucket reference — sensitive, masked by role wherever displayed or exported.</summary>
    public string StoragePathOrReference { get; set; } = string.Empty;

    public DateTime? FirstDiscoveredAtUtc { get; set; }

    public CopyVerificationStatus VerificationStatus { get; set; } = CopyVerificationStatus.NeverVerified;
    public DateTime? LastVerifiedAtUtc { get; set; }
    public string LastVerifiedBy { get; set; } = string.Empty;
    public string VerificationNotes { get; set; } = string.Empty;

    /// <summary>Optional reference to a legal hold covering this copy — a pointer/ticket id, not a legal determination made by this app.</summary>
    public string LegalHoldReference { get; set; } = string.Empty;

    public string Notes { get; set; } = string.Empty;

    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAtUtc { get; set; } = DateTime.UtcNow;
    public string UpdatedBy { get; set; } = string.Empty;
}
