using DBTracker.Core.Entities;

namespace DBTracker.Core.Services;

/// <summary>
/// Applies an approved ContactCorrectionRequest's proposed fields onto an instance (U21) — only a
/// field the requester actually filled in is changed; an empty proposed value means "no change to
/// this field", the same "present vs. absent" convention U17's CSV importer uses so a lean request
/// can't silently blank out fields the requester didn't intend to touch. Pure (no I/O) so the rule
/// is unit-testable without a database; the caller is responsible for persisting the result and
/// recording the change (audit log / instance change log).
/// </summary>
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

    /// <summary>Whether this request actually proposes any field change — an all-empty request is rejected before creation rather than silently stored as a no-op.</summary>
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
