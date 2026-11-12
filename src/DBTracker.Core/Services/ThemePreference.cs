namespace DBTracker.Core.Services;

/// <summary>The viewer's colour theme: System (follow the OS), Dark or Light.</summary>
/// <remarks>
/// Kept in a cookie rather than on the user, so the sign-in page can honour it before anyone is signed in and no
/// schema change is needed. App.razor reads it and writes the value onto &lt;html data-theme&gt;, so the first paint is
/// already in the right theme without a script. Anything missing or unknown falls back to Dark, the console default.
/// </remarks>
public static class ThemePreference
{
    public const string CookieName = "dbt-theme";

    public const string System = "system";
    public const string Dark = "dark";
    public const string Light = "light";

    public const string Default = Dark;

    public static readonly IReadOnlyList<string> All = [System, Dark, Light];

    /// <summary>The stored value as one of System, Dark or Light; anything else reads as the default.</summary>
    public static string Normalize(string? value) => value?.Trim().ToLowerInvariant() switch
    {
        System => System,
        Light => Light,
        Dark => Dark,
        _ => Default,
    };
}
