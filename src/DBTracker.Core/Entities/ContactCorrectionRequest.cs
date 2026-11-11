using DBTracker.Core.Enums;

namespace DBTracker.Core.Entities;

/// <summary>A request from any signed-in user to fix an instance's contact details. Only CanEditPolicy roles approve it, and only filled-in proposed fields are applied.</summary>
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
