using DBTracker.Core.Enums;

namespace DBTracker.Core.Entities;

/// <summary>
/// A "this contact info looks wrong" request (U21) — any authenticated viewer (e.g. Dev, who isn't
/// authorized to edit the instance directly) can submit one; only an authorized reviewer
/// (Roles.CanEditPolicy) can approve or reject it. Approving applies only the proposed fields that
/// were actually filled in — an empty proposed field means "no change", same convention U17's CSV
/// importer uses for "field present but absent from this row".
/// </summary>
public class ContactCorrectionRequest
{
    public int Id { get; set; }

    public int DatabaseInstanceId { get; set; }
    public DatabaseInstance? DatabaseInstance { get; set; }

    public string RequestedByUserId { get; set; } = string.Empty;
    public string RequestedByName { get; set; } = string.Empty;
    public DateTime RequestedAtUtc { get; set; } = DateTime.UtcNow;

    public string ProposedContactName { get; set; } = string.Empty;
    public string ProposedContactEmail { get; set; } = string.Empty;
    public string ProposedContactPhone { get; set; } = string.Empty;
    public string ProposedBackupContactName { get; set; } = string.Empty;
    public string ProposedBackupContactEmail { get; set; } = string.Empty;
    public string ProposedBackupContactPhone { get; set; } = string.Empty;

    /// <summary>Why the requester believes the current contact is wrong — required.</summary>
    public string Note { get; set; } = string.Empty;

    public ContactCorrectionStatus Status { get; set; } = ContactCorrectionStatus.Pending;

    public string ReviewedByUserId { get; set; } = string.Empty;
    public string ReviewedByName { get; set; } = string.Empty;
    public DateTime? ReviewedAtUtc { get; set; }
    public string ReviewNote { get; set; } = string.Empty;
}
