using DBTracker.Core.Entities;

namespace DBTracker.Core.Services;

/// <summary>Applies an approved contact correction. Only fields the requester filled in change, so a sparse request can't blank other fields; the caller persists and audits the result.</summary>
public static class ContactCorrectionApplier
{
    public static void Apply(DatabaseInstance instance, ContactCorrectionRequest request)
    {
        ApplyIfPresent(request.ProposedContactName, v => instance.ContactName = v);
        ApplyIfPresent(request.ProposedContactEmail, v => instance.ContactEmail = v);
        ApplyIfPresent(request.ProposedContactPhone, v => instance.ContactPhone = v);
        ApplyIfPresent(request.ProposedBackupContactName, v => instance.BackupContactName = v);
        ApplyIfPresent(request.ProposedBackupContactEmail, v => instance.BackupContactEmail = v);
        ApplyIfPresent(request.ProposedBackupContactPhone, v => instance.BackupContactPhone = v);
    }

    /// <summary>Whether the request proposes any change; an all-empty request is rejected before it's created.</summary>
    public static bool HasAnyProposedChange(ContactCorrectionRequest request) =>
        !string.IsNullOrWhiteSpace(request.ProposedContactName) ||
        !string.IsNullOrWhiteSpace(request.ProposedContactEmail) ||
        !string.IsNullOrWhiteSpace(request.ProposedContactPhone) ||
        !string.IsNullOrWhiteSpace(request.ProposedBackupContactName) ||
        !string.IsNullOrWhiteSpace(request.ProposedBackupContactEmail) ||
        !string.IsNullOrWhiteSpace(request.ProposedBackupContactPhone);

    private static void ApplyIfPresent(string proposedValue, Action<string> set)
    {
        if (!string.IsNullOrWhiteSpace(proposedValue))
        {
            set(proposedValue);
        }
    }
}
