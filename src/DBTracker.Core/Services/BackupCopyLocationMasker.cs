namespace DBTracker.Core.Services;

/// <summary>Masks a backup copy's raw storage path for roles that shouldn't see it. The single chokepoint for every view and export.</summary>
public static class BackupCopyLocationMasker
{
    public const string MaskedPlaceholder = "•••• (masked — Admin/DBA only)";

    public static string Mask(string? storagePathOrReference, bool canViewSensitivePaths)
    {
        if (string.IsNullOrWhiteSpace(storagePathOrReference))
        {
            return string.Empty;
        }

        return canViewSensitivePaths ? storagePathOrReference : MaskedPlaceholder;
    }
}
