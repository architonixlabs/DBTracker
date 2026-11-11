using DBTracker.Core.Enums;

namespace DBTracker.Core.Services;

/// <summary>How complete and confirmed an instance's ownership information is. Informational only.</summary>
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
