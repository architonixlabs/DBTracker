using System.Globalization;

namespace DBTracker.Core.Services;

/// <summary>Masks a cost estimate's figures for roles that shouldn't see them, on every view and export.</summary>
public static class CostEvidenceMasker
{
    public const string MaskedPlaceholder = "•••• (masked: Admin/ITManager only)";

    // Invariant culture so 123456.78 renders the same on every host locale.
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
