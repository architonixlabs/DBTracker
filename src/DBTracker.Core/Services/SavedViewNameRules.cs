namespace DBTracker.Core.Services;

/// <summary>Validation for saved-view names, shared by save and rename. Matches the 100-character column.</summary>
public static class SavedViewNameRules
{
    public const int MaxLength = 100;

    /// <summary>Trims and collapses inner runs of whitespace, so "My  view " and "My view" are the same name.</summary>
    public static string Normalize(string? name) =>
        string.Join(' ', (name ?? "").Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries));

    /// <summary>Null when the name is usable, otherwise a message for the user.</summary>
    public static string? Validate(string? name)
    {
        var normalized = Normalize(name);
        if (normalized.Length == 0)
        {
            return "Enter a name for the view.";
        }
        if (normalized.Length > MaxLength)
        {
            return $"Use {MaxLength} characters or fewer.";
        }

        return null;
    }
}
