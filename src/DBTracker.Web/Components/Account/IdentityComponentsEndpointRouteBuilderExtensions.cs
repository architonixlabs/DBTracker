using System.Security.Claims;
using DBTracker.Infrastructure.Identity;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;

namespace Microsoft.AspNetCore.Routing;

internal static class IdentityComponentsEndpointRouteBuilderExtensions
{
    public static IEndpointConventionBuilder MapIdentityLogoutEndpoint(this IEndpointRouteBuilder endpoints)
    {
        var accountGroup = endpoints.MapGroup("/Account");

        accountGroup.MapPost("/Logout", async (
            ClaimsPrincipal user,
            [FromServices] SignInManager<ApplicationUser> signInManager,
            [FromForm] string returnUrl) =>
        {
            await signInManager.SignOutAsync();
            // returnUrl already carries a leading "/" (e.g. "/Account/Login"); prepending another
            // "~/" produced "~//Account/Login", which IsLocalUrl rejects as protocol-relative.
            return TypedResults.LocalRedirect(returnUrl.StartsWith('/') ? $"~{returnUrl}" : $"~/{returnUrl}");
        });

        return accountGroup;
    }
}
