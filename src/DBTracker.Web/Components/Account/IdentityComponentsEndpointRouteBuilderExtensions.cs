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
            [FromForm] string returnUrl,
            HttpContext context) =>
        {
            await signInManager.SignOutAsync();
            // On a shared or borrowed device, leave nothing of the session behind: cookies, storage and cached pages.
            context.Response.Headers["Clear-Site-Data"] = "\"cache\", \"cookies\", \"storage\"";
            // returnUrl already starts with "/"; prefixing "~/" gave "~//Account/Login", which IsLocalUrl rejects as protocol-relative.
            // The theme cookie goes too; the sign-in page puts it back from ?theme= (see ThemeCookie).
            var target = DBTracker.Web.Services.ThemeCookie.CarryOver(context, returnUrl.StartsWith('/') ? returnUrl : $"/{returnUrl}");
            return TypedResults.LocalRedirect($"~{target}");
        }).RequireAuthorization();

        return accountGroup;
    }
}
