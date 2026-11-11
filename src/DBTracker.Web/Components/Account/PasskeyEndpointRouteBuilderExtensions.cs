using System.Text.Json;
using DBTracker.Infrastructure.Identity;
using Microsoft.AspNetCore.Antiforgery;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;

namespace Microsoft.AspNetCore.Routing;

/// <summary>G26 passkey endpoints, called by fetch from wwwroot/js/passkey-login.js and passkeys.js.</summary>
/// <remarks>
/// They run as plain HTTP requests, not over the Blazor circuit, because Identity keeps the WebAuthn challenge in a short-lived auth cookie
/// and signs the user in with another cookie. Each one validates the antiforgery token from the RequestVerificationToken header, as the
/// .NET 10 Blazor Web App template does. All cryptographic checks (challenge, origin, RP ID, signature, sign count) are Identity's.
/// </remarks>
internal static class PasskeyEndpointRouteBuilderExtensions
{
    public sealed record RegisterRequest(JsonElement Credential, string? Name);

    public sealed record SignInRequest(JsonElement Credential, string? ReturnUrl);

    public static IEndpointConventionBuilder MapPasskeyEndpoints(this IEndpointRouteBuilder endpoints)
    {
        var group = endpoints.MapGroup("/Account/Passkeys");

        group.MapPost("/CreationOptions", async (
            HttpContext context,
            [FromServices] UserManager<ApplicationUser> userManager,
            [FromServices] SignInManager<ApplicationUser> signInManager,
            [FromServices] IAntiforgery antiforgery) =>
        {
            if (!await antiforgery.IsRequestValidAsync(context))
            {
                return ExpiredPage();
            }
            var user = await userManager.GetUserAsync(context.User);
            if (user is null)
            {
                return Results.Unauthorized();
            }
            // A temporary password is the Administrator's, not the user's: they set their own before adding another way in.
            if (user.MustChangePassword)
            {
                return Message(StatusCodes.Status409Conflict, "Change your temporary password before adding a passkey.");
            }

            var name = user.Email ?? user.UserName ?? user.Id;
            var optionsJson = await signInManager.MakePasskeyCreationOptionsAsync(new()
            {
                Id = user.Id,
                Name = name,
                DisplayName = string.IsNullOrWhiteSpace(user.DisplayName) ? name : user.DisplayName,
            });
            return Results.Content(optionsJson, "application/json");
        }).RequireAuthorization();

        group.MapPost("/Register", async (
            HttpContext context,
            [FromBody] RegisterRequest request,
            [FromServices] UserManager<ApplicationUser> userManager,
            [FromServices] SignInManager<ApplicationUser> signInManager,
            [FromServices] PasskeyService passkeys,
            [FromServices] IAntiforgery antiforgery) =>
        {
            if (!await antiforgery.IsRequestValidAsync(context))
            {
                return ExpiredPage();
            }
            var user = await userManager.GetUserAsync(context.User);
            if (user is null)
            {
                return Results.Unauthorized();
            }

            PasskeyAttestationResult attestation;
            try
            {
                attestation = await signInManager.PerformPasskeyAttestationAsync(request.Credential.GetRawText());
            }
            catch (InvalidOperationException)
            {
                // No creation options in flight: the challenge cookie expired or was never set.
                return Message(StatusCodes.Status400BadRequest, "That request timed out. Select Add a passkey again.");
            }

            if (!attestation.Succeeded)
            {
                return Message(StatusCodes.Status400BadRequest, "The passkey couldn't be verified, so it wasn't added. Try again.");
            }
            // The options were made for the signed-in user; refuse a credential created for anyone else.
            if (attestation.UserEntity.Id != user.Id)
            {
                return Message(StatusCodes.Status400BadRequest, "That passkey was created for a different account, so it wasn't added.");
            }

            var saved = await passkeys.AddAsync(user, attestation.Passkey, request.Name);
            return saved.Succeeded
                ? Results.Ok(new { message = "Passkey added. You can use it the next time you sign in." })
                : Message(StatusCodes.Status500InternalServerError, saved.Error!);
        }).RequireAuthorization();

        group.MapPost("/RequestOptions", async (
            HttpContext context,
            [FromServices] SignInManager<ApplicationUser> signInManager,
            [FromServices] IAntiforgery antiforgery) =>
        {
            if (!await antiforgery.IsRequestValidAsync(context))
            {
                return ExpiredPage();
            }
            // No user: allowCredentials stays empty so the browser offers any discoverable passkey for this site.
            var optionsJson = await signInManager.MakePasskeyRequestOptionsAsync(null);
            return Results.Content(optionsJson, "application/json");
        }).AllowAnonymous();

        group.MapPost("/SignIn", async (
            HttpContext context,
            [FromBody] SignInRequest request,
            [FromServices] UserManager<ApplicationUser> userManager,
            [FromServices] SignInManager<ApplicationUser> signInManager,
            [FromServices] ILoggerFactory loggerFactory,
            [FromServices] IAntiforgery antiforgery) =>
        {
            if (!await antiforgery.IsRequestValidAsync(context))
            {
                return ExpiredPage();
            }
            var credentialJson = request.Credential.GetRawText();

            Microsoft.AspNetCore.Identity.SignInResult result;
            try
            {
                // Runs PreSignInCheck, so AppSignInManager.CanSignInAsync refuses deactivated accounts with NotAllowed.
                result = await signInManager.PasskeySignInAsync(credentialJson);
            }
            catch (InvalidOperationException)
            {
                return Message(StatusCodes.Status400BadRequest, "That sign-in request timed out. Try the passkey again.");
            }

            var credentialId = request.Credential.TryGetProperty("id", out var idElement) ? PasskeyService.DecodeCredentialId(idElement.GetString()) : null;
            var user = credentialId is null ? null : await userManager.FindByPasskeyIdAsync(credentialId);

            if (result.Succeeded && user is not null)
            {
                loggerFactory.CreateLogger("DBTracker.Passkeys").LogInformation("User signed in with a passkey.");
                // Same rule as password sign-in; MustChangePasswordGuard also enforces it on every page.
                var redirect = user.MustChangePassword ? "/account/change-password?Required=true" : DBTracker.Core.Services.ReturnUrlPolicy.LocalOrRoot(request.ReturnUrl);
                return Results.Ok(new { redirectUrl = redirect });
            }
            if (result.IsNotAllowed)
            {
                return Message(StatusCodes.Status403Forbidden, "This account has been deactivated. Contact an Administrator.");
            }
            if (result.IsLockedOut)
            {
                return Message(StatusCodes.Status403Forbidden, "This account is locked. Try again later or contact an Administrator.");
            }
            if (user is null)
            {
                // 404 lets the page tell the passkey provider to forget this credential (Signal API).
                return Message(StatusCodes.Status404NotFound,
                    "This passkey isn't registered with DBTracker any more. Sign in with your password, then add a new passkey.");
            }
            return Message(StatusCodes.Status400BadRequest, "Passkey sign-in didn't work. Try again, or sign in with your password.");
        }).AllowAnonymous();

        return group;
    }

    private static IResult Message(int status, string message) => Results.Json(new { message }, statusCode: status);

    private static IResult ExpiredPage() =>
        Message(StatusCodes.Status400BadRequest, "This page has been open too long. Reload it and try again.");
}
