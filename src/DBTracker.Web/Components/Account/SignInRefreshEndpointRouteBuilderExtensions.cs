using DBTracker.Infrastructure.Identity;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;

namespace Microsoft.AspNetCore.Routing;

/// <summary>Keeps a user signed in after they change their own password.</summary>
/// <remarks>
/// Changing a password rotates the security stamp, which is what ends other sessions. The change happens over the Blazor circuit, which
/// can't write cookies, so the browser's own cookie still carried the old stamp and was rejected at the next stamp check: the user was
/// signed out. After a successful change the page navigates here with a one-time token (user id and the new stamp, protected and valid
/// for two minutes). This request re-issues the cookie with the new stamp. A token whose stamp is no longer current (a second change, a
/// role change, a deactivation) is refused, so it can't be replayed to undo any of those.
/// </remarks>
internal static class SignInRefreshEndpointRouteBuilderExtensions
{
    public const string Path = "/Account/RefreshSignIn";
    private const string Purpose = "DBTracker.SignInRefresh.v1";
    private static readonly TimeSpan Lifetime = TimeSpan.FromMinutes(2);

    /// <summary>The local URL to navigate to (with a full page load) after the user's password changed.</summary>
    public static string CreateUrl(IDataProtectionProvider provider, string userId, string securityStamp, string returnUrl)
    {
        var token = provider.CreateProtector(Purpose).ToTimeLimitedDataProtector()
            .Protect(DBTracker.Core.Services.SignInRefreshToken.Format(userId, securityStamp), Lifetime);
        return $"{Path}?token={Uri.EscapeDataString(token)}&returnUrl={Uri.EscapeDataString(DBTracker.Core.Services.ReturnUrlPolicy.LocalOrRoot(returnUrl))}";
    }

    public static IEndpointConventionBuilder MapSignInRefreshEndpoint(this IEndpointRouteBuilder endpoints) =>
        endpoints.MapGet(Path, async (
            HttpContext context,
            [FromQuery] string? token,
            [FromQuery] string? returnUrl,
            [FromServices] IDataProtectionProvider provider,
            [FromServices] UserManager<ApplicationUser> userManager,
            [FromServices] SignInManager<ApplicationUser> signInManager) =>
        {
            context.Response.Headers.CacheControl = "no-store";
            var target = DBTracker.Core.Services.ReturnUrlPolicy.LocalOrRoot(returnUrl);
            string payload;
            try
            {
                payload = provider.CreateProtector(Purpose).ToTimeLimitedDataProtector().Unprotect(token ?? "");
            }
            catch (System.Security.Cryptography.CryptographicException)
            {
                return Results.LocalRedirect("/Account/Login?ReturnUrl=" + Uri.EscapeDataString(target));
            }

            if (DBTracker.Core.Services.SignInRefreshToken.Parse(payload) is not { } parsed
                || await userManager.FindByIdAsync(parsed.UserId) is not { IsActive: true } user
                || await userManager.GetSecurityStampAsync(user) != parsed.SecurityStamp)
            {
                return Results.LocalRedirect("/Account/Login?ReturnUrl=" + Uri.EscapeDataString(target));
            }

            // Keep "remember me" as the user chose it when they signed in, if the old cookie still says so.
            var current = await context.AuthenticateAsync(IdentityConstants.ApplicationScheme);
            var persistent = current.Succeeded && current.Properties?.IsPersistent == true;
            await signInManager.SignInAsync(user, isPersistent: persistent, authenticationMethod: "pwd");
            return Results.LocalRedirect(target);
        }).AllowAnonymous();
}
