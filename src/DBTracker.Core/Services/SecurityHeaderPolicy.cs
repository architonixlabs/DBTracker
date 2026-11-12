namespace DBTracker.Core.Services;

/// <summary>The security headers every response carries, built from a per-request script nonce.</summary>
/// <remarks>
/// Scripts run only with the nonce ('strict-dynamic' lets blazor.web.js load its own modules). base-uri is 'self', not
/// 'none', because Blazor needs its &lt;base href="/"&gt;. Styles aren't restricted yet: the pages use inline style
/// attributes, which would need 'unsafe-inline'. Trusted Types runs report-only until Blazor's DOM writes are proven
/// compatible. Reports go to /csp-reports, which logs them without query strings.
/// </remarks>
public static class SecurityHeaderPolicy
{
    public const string ReportPath = "/csp-reports";
    public const string ReportGroup = "csp";

    public static string ContentSecurityPolicy(string nonce) => string.Join("; ",
        $"script-src 'nonce-{nonce}' 'strict-dynamic' 'report-sample'",
        "object-src 'none'",
        "base-uri 'self'",
        "frame-ancestors 'none'",
        "form-action 'self'",
        $"report-to {ReportGroup}");

    public static string ContentSecurityPolicyReportOnly() => string.Join("; ",
        "require-trusted-types-for 'script'",
        $"report-to {ReportGroup}");

    public static IReadOnlyList<KeyValuePair<string, string>> Headers(string nonce) =>
    [
        new("Content-Security-Policy", ContentSecurityPolicy(nonce)),
        new("Content-Security-Policy-Report-Only", ContentSecurityPolicyReportOnly()),
        new("Reporting-Endpoints", $"{ReportGroup}=\"{ReportPath}\""),
        new("X-Content-Type-Options", "nosniff"),
        new("X-Frame-Options", "DENY"),
        new("Referrer-Policy", "strict-origin-when-cross-origin"),
        // The QR sign-in is scanned with the phone's own camera app, so the page itself needs no camera.
        new("Permissions-Policy", "camera=(), geolocation=(), microphone=(), payment=(), usb=()"),
        new("Cross-Origin-Opener-Policy", "same-origin-allow-popups"),
        new("Cross-Origin-Resource-Policy", "same-origin"),
    ];

    /// <summary>A report's URL without its query string or fragment, so tokens and reset codes never reach the log.</summary>
    public static string StripQuery(string? url)
    {
        if (string.IsNullOrEmpty(url))
        {
            return "";
        }

        var cut = url.IndexOfAny(['?', '#']);
        return cut >= 0 ? url[..cut] : url;
    }
}
