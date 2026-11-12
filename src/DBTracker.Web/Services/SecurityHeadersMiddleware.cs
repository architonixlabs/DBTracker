using System.Security.Cryptography;
using System.Text.Json;
using DBTracker.Core.Services;

namespace DBTracker.Web.Services;

/// <summary>Adds the security headers from SecurityHeaderPolicy with a fresh script nonce per request, and receives
/// CSP violation reports.</summary>
public static class SecurityHeadersMiddleware
{
    public const string NonceKey = "dbtracker.csp-nonce";
    private const int MaxReportBytes = 16 * 1024;

    public static IApplicationBuilder UseSecurityHeaders(this IApplicationBuilder app) =>
        app.Use(async (context, next) =>
        {
            var nonce = Convert.ToBase64String(RandomNumberGenerator.GetBytes(16));
            context.Items[NonceKey] = nonce;
            context.Response.OnStarting(() =>
            {
                foreach (var (name, value) in SecurityHeaderPolicy.Headers(nonce))
                {
                    context.Response.Headers[name] = value;
                }
                return Task.CompletedTask;
            });
            await next();
        });

    /// <summary>Logs each violation's directive, blocked origin and page path (never query strings or report bodies).</summary>
    public static IEndpointRouteBuilder MapCspReports(this IEndpointRouteBuilder endpoints)
    {
        endpoints.MapPost(SecurityHeaderPolicy.ReportPath, async (HttpRequest request, ILoggerFactory loggers) =>
        {
            var logger = loggers.CreateLogger("DBTracker.CspReports");
            if (request.ContentLength is > MaxReportBytes)
            {
                return Results.StatusCode(StatusCodes.Status413PayloadTooLarge);
            }

            try
            {
                using var doc = await JsonDocument.ParseAsync(request.Body);
                foreach (var body in ReportBodies(doc.RootElement))
                {
                    logger.LogWarning("CSP violation: {Directive} blocked {Blocked} on {Page} ({Disposition})",
                        Get(body, "effectiveDirective", "effective-directive", "violated-directive"),
                        SecurityHeaderPolicy.StripQuery(Get(body, "blockedURL", "blocked-uri")),
                        SecurityHeaderPolicy.StripQuery(Get(body, "documentURL", "document-uri")),
                        Get(body, "disposition"));
                }
            }
            catch (JsonException)
            {
                return Results.BadRequest();
            }

            return Results.NoContent();
        }).AllowAnonymous().DisableAntiforgery();

        return endpoints;
    }

    // Reporting API sends an array of { type, body }; the older report-uri format sends { "csp-report": { ... } }.
    private static IEnumerable<JsonElement> ReportBodies(JsonElement root)
    {
        if (root.ValueKind == JsonValueKind.Array)
        {
            foreach (var item in root.EnumerateArray().Take(20))
            {
                if (item.TryGetProperty("body", out var body))
                {
                    yield return body;
                }
            }
        }
        else if (root.ValueKind == JsonValueKind.Object && root.TryGetProperty("csp-report", out var legacy))
        {
            yield return legacy;
        }
    }

    private static string Get(JsonElement body, params string[] names)
    {
        foreach (var name in names)
        {
            if (body.TryGetProperty(name, out var value) && value.ValueKind == JsonValueKind.String)
            {
                var text = value.GetString() ?? "";
                return text.Length > 200 ? text[..200] : text;
            }
        }
        return "";
    }
}
