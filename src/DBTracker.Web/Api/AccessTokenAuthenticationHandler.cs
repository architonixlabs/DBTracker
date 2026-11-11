using System.Security.Claims;
using System.Text.Encodings.Web;
using DBTracker.Infrastructure.Identity;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Diagnostics;
using Microsoft.Extensions.Options;

namespace DBTracker.Web.Api;

public static class AccessTokenDefaults
{
    public const string Scheme = "DBTrackerAccessToken";
    public const string Policy = "AccessToken";
}

/// <summary>Bearer personal access tokens for /api/*. Never falls back to the Identity cookie and never redirects.</summary>
public class AccessTokenAuthenticationHandler(
    IOptionsMonitor<AuthenticationSchemeOptions> options,
    ILoggerFactory logger,
    UrlEncoder encoder,
    PersonalAccessTokenService tokens)
    : AuthenticationHandler<AuthenticationSchemeOptions>(options, logger, encoder)
{
    protected override async Task<AuthenticateResult> HandleAuthenticateAsync()
    {
        var header = Request.Headers.Authorization.ToString();
        if (string.IsNullOrEmpty(header) || !header.StartsWith("Bearer ", StringComparison.OrdinalIgnoreCase))
        {
            return AuthenticateResult.NoResult();
        }

        var user = await tokens.ValidateAsync(header["Bearer ".Length..].Trim(), Context.RequestAborted);
        if (user is null)
        {
            return AuthenticateResult.Fail("Invalid access token.");
        }

        var claims = new List<Claim>
        {
            new(ClaimTypes.NameIdentifier, user.UserId),
            new(ClaimTypes.Name, user.Email),
        };
        claims.AddRange(user.Roles.Select(role => new Claim(ClaimTypes.Role, role)));
        var principal = new ClaimsPrincipal(new ClaimsIdentity(claims, Scheme.Name));
        return AuthenticateResult.Success(new AuthenticationTicket(principal, Scheme.Name));
    }

    protected override Task HandleChallengeAsync(AuthenticationProperties properties) =>
        WriteErrorAsync(StatusCodes.Status401Unauthorized, "invalid_token", "A valid personal access token is required.");

    protected override Task HandleForbiddenAsync(AuthenticationProperties properties) =>
        WriteErrorAsync(StatusCodes.Status403Forbidden, "forbidden", "This token can't access that resource.");

    private async Task WriteErrorAsync(int status, string error, string message)
    {
        // Otherwise the app's status-code pages re-execute to /not-found, which requires sign-in and becomes a login redirect.
        if (Context.Features.Get<IStatusCodePagesFeature>() is { } statusPages)
        {
            statusPages.Enabled = false;
        }
        Response.StatusCode = status;
        if (status == StatusCodes.Status401Unauthorized)
        {
            Response.Headers.WWWAuthenticate = "Bearer";
        }
        await Response.WriteAsJsonAsync(new { error, message });
    }
}
