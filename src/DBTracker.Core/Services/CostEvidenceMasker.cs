using System.Globalization;

namespace DBTracker.Core.Services;

/// <summary>D21 — masks a cost estimate's commercial figures for roles that shouldn't see them, on every view and export alike (same chokepoint pattern as D17's BackupCopyLocationMasker).</summary>
public static class CostEvidenceMasker
{
    public const string MaskedPlaceholder = "•••• (masked — Admin/ITManager only)";

    // Invariant culture deliberately, not the server's ambient thread culture — a figure like
    // 123456.78 must render the same way (123,456.78) regardless of the host machine's locale.
    public static string MaskAmount(decimal amount, string currency, bool canViewCommercial) =>
        canViewCommercial ? $"{amount.ToString("N2", CultureInfo.InvariantCulture)} {currency}" : MaskedPlaceholder;

    public static string MaskText(string? value, bool canViewCommercial)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return string.Empty;
        }

        return canViewCommercial ? value : MaskedPlaceholder;
    }
}
