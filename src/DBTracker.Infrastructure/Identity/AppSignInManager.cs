using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace DBTracker.Infrastructure.Identity;

/// <summary>
/// A deactivated account (U16) must never be able to sign in again, even with the correct password
/// — CanSignInAsync is ASP.NET Core Identity's own extensibility point for exactly this, so
/// PasswordSignInAsync comes back as a plain "not allowed" rather than this app re-implementing the
/// whole sign-in pipeline.
/// </summary>
public class AppSignInManager(
    UserManager<ApplicationUser> userManager,
    IHttpContextAccessor contextAccessor,
    IUserClaimsPrincipalFactory<ApplicationUser> claimsFactory,
    IOptions<IdentityOptions> optionsAccessor,
    ILogger<SignInManager<ApplicationUser>> logger,
    IAuthenticationSchemeProvider schemes,
    IUserConfirmation<ApplicationUser> confirmation)
    : SignInManager<ApplicationUser>(userManager, contextAccessor, claimsFactory, optionsAccessor, logger, schemes, confirmation)
{
    public override Task<bool> CanSignInAsync(ApplicationUser user) =>
        Task.FromResult(user.IsActive);
}
