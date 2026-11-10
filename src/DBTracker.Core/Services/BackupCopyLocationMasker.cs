namespace DBTracker.Core.Services;

/// <summary>D17 — masks a backup-copy's raw storage path/reference for roles that shouldn't see it, on every view and every CSV/report export alike (one chokepoint, not a per-page reimplementation).</summary>
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
