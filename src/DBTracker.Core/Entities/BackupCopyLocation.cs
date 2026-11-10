using DBTracker.Core.Enums;

namespace DBTracker.Core.Entities;

/// <summary>
/// D17 — one storage/copy location a recovery depends on (primary, offsite, immutable-flagged).
/// FirstDiscoveredAtUtc captures the historical "we saw this once" fact (reusing U01's evidence-level
/// framing); VerificationStatus/LastVerifiedAtUtc are a separate, current-state claim — the two are
/// never conflated, so a copy nobody has checked in months still shows its last known state honestly
/// rather than appearing freshly verified just because it was discovered long ago.
/// StoragePathOrReference is masked by role on every view/export (BackupCopyLocationMasker) —
/// Angela/Marcus/Dev never see the raw path, only Admin/DBA do.
/// </summary>
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
