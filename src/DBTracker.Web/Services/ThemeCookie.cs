using DBTracker.Core.Services;

namespace DBTracker.Web.Services;

/// <summary>Keeps the theme choice across sign-out.</summary>
/// <remarks>
/// Logout sends Clear-Site-Data "cookies", which also wipes dbt-theme, so the sign-in page would flash back to the dark
/// default. The logout endpoint therefore carries the choice to the sign-in page as ?theme=, and this middleware writes
/// the cookie again there. The theme is a device preference, not part of the session, so keeping it is safe.
/// </remarks>
public static class ThemeCookie
{
    public const string QueryKey = "theme";
    public const string RequestItemKey = "dbtracker.theme";

    /// <summary>The theme for this request: one carried over on the sign-in page, else the cookie, else the default.</summary>
    public static string For(HttpContext context) =>
        context.Items[RequestItemKey] as string ?? ThemePreference.Normalize(context.Request.Cookies[ThemePreference.CookieName]);

    /// <summary>The sign-in URL with the current theme attached, when the viewer has made a choice.</summary>
    public static string CarryOver(HttpContext context, string url)
    {
        var stored = context.Request.Cookies[ThemePreference.CookieName];
        return stored is null
            ? url
            : Microsoft.AspNetCore.WebUtilities.QueryHelpers.AddQueryString(url, QueryKey, ThemePreference.Normalize(stored));
    }

    public static IApplicationBuilder UseThemeCookieCarryOver(this IApplicationBuilder app) =>
        app.Use(async (context, next) =>
        {
            if (context.Request.Path.StartsWithSegments("/Account/Login", StringComparison.OrdinalIgnoreCase) &&
                context.Request.Query.TryGetValue(QueryKey, out var carried) &&
                !context.Request.Cookies.ContainsKey(ThemePreference.CookieName))
            {
                var theme = ThemePreference.Normalize(carried.ToString());
                context.Items[RequestItemKey] = theme;
                context.Response.Cookies.Append(ThemePreference.CookieName, theme, new CookieOptions
                {
                    Path = "/",
                    MaxAge = TimeSpan.FromDays(365),
                    SameSite = SameSiteMode.Lax,
                    Secure = context.Request.IsHttps,
                    // dbtracker.js rewrites it when the viewer changes theme.
                    HttpOnly = false,
                    IsEssential = true,
                });
            }
            await next();
        });
}
