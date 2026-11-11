using DBTracker.Infrastructure.Identity;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using QRCoder;

namespace Microsoft.AspNetCore.Routing;

/// <summary>QR phone sign-in endpoints. create/qr/status/redeem are anonymous because the desktop isn't signed in yet; only approve/deny require an authenticated phone.</summary>
internal static class DeviceLinkEndpointRouteBuilderExtensions
{
    public static IEndpointConventionBuilder MapDeviceLinkEndpoints(this IEndpointRouteBuilder endpoints)
    {
        var group = endpoints.MapGroup("/device-link");

        // Called via JS fetch from the anonymous desktop page, so there's no antiforgery token, and no session for a forged request to ride on.
        group.MapPost("/create", async ([FromServices] DeviceLinkService service) =>
        {
            var token = await service.CreateTokenAsync();
            return TypedResults.Ok(new { token });
        }).AllowAnonymous().DisableAntiforgery();

        group.MapGet("/qr/{token}", (string token, HttpContext context) =>
        {
            var url = $"{context.Request.Scheme}://{context.Request.Host}/Account/LinkDevice?token={token}";
            using var generator = new QRCodeGenerator();
            using var data = generator.CreateQrCode(url, QRCodeGenerator.ECCLevel.M);
            var png = new PngByteQRCode(data).GetGraphic(8);
            return Results.File(png, "image/png");
        }).AllowAnonymous();

        group.MapGet("/status/{token}", async (string token, [FromServices] DeviceLinkService service) =>
        {
            var status = await service.GetStatusAsync(token);
            return status is null
                ? Results.NotFound()
                : TypedResults.Ok(new { status = status.ToString() });
        }).AllowAnonymous();

        group.MapPost("/approve", async (
            HttpContext context,
            [FromServices] DeviceLinkService service,
            [FromServices] UserManager<ApplicationUser> userManager,
            [FromForm] string token) =>
        {
            var user = await userManager.GetUserAsync(context.User);
            if (user is null)
            {
                return Results.Unauthorized();
            }
            var ok = await service.ApproveAsync(token, user.Id);
            return ok ? Results.LocalRedirect("~/Account/LinkDeviceDone?outcome=approved") : Results.BadRequest();
        }).RequireAuthorization();

        group.MapPost("/deny", async (
            [FromServices] DeviceLinkService service,
            [FromForm] string token) =>
        {
            var ok = await service.DenyAsync(token);
            return ok ? Results.LocalRedirect("~/Account/LinkDeviceDone?outcome=denied") : Results.BadRequest();
        }).RequireAuthorization();

        group.MapPost("/redeem/{token}", async (
            string token,
            [FromServices] DeviceLinkService service,
            [FromServices] UserManager<ApplicationUser> userManager,
            [FromServices] SignInManager<ApplicationUser> signInManager) =>
        {
            var userId = await service.RedeemAsync(token);
            if (userId is null)
            {
                return Results.BadRequest();
            }

            var user = await userManager.FindByIdAsync(userId);
            if (user is null)
            {
                return Results.BadRequest();
            }

            await signInManager.SignInAsync(user, isPersistent: false, authenticationMethod: "device-link");
            return Results.Ok();
        }).AllowAnonymous().DisableAntiforgery();

        return group;
    }
}
