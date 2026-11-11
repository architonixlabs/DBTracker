using System.Text.RegularExpressions;

namespace DBTracker.Core.Services.Install;

public static partial class DatabaseNameValidator
{
    private static readonly HashSet<string> SystemDatabases = new(StringComparer.OrdinalIgnoreCase) { "master", "model", "msdb", "tempdb" };

    /// <summary>Null when the name is usable. Stricter than SQL Server needs, so the name is also safe in file paths and logs.</summary>
    public static string? Validate(string? name)
    {
        if (string.IsNullOrWhiteSpace(name))
        {
            return "Enter a database name.";
        }

        if (name.Length > 128)
        {
            return "Database names can be at most 128 characters.";
        }

        if (!AllowedName().IsMatch(name))
        {
            return "Use letters, digits, underscores, hyphens or dots, starting with a letter, digit or underscore.";
        }

        if (SystemDatabases.Contains(name))
        {
            return $"{name} is a SQL Server system database. Choose another name.";
        }

        return null;
    }

    [GeneratedRegex(@"^[A-Za-z0-9_][A-Za-z0-9_\-\.]*$")]
    private static partial Regex AllowedName();
}
