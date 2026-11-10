using DBTracker.Core.Enums;

namespace DBTracker.Core.Services;

/// <summary>
/// Classifies how complete/confirmed an instance's ownership information is (U21) — pure so the
/// Missing/Unconfirmed/Confirmed rule is unit-testable without a database. Purely informational: it
/// never changes what a role is authorized to do, only flags instances an owner should look at.
/// </summary>
public static class OwnershipStatusClassifier
{
    public static OwnershipStatus Classify(
        int? ownerTeamId, string contactEmail, string backupContactEmail, DateTime? ownershipConfirmedAtUtc)
    {
        var hasAnyOwnerInfo = ownerTeamId is not null ||
            !string.IsNullOrWhiteSpace(contactEmail) ||
            !string.IsNullOrWhiteSpace(backupContactEmail);

        if (!hasAnyOwnerInfo)
        {
            return OwnershipStatus.Missing;
        }

        return ownershipConfirmedAtUtc is null ? OwnershipStatus.Unconfirmed : OwnershipStatus.Confirmed;
    }
}
