using Microsoft.AspNetCore.Identity;

namespace DBTracker.Infrastructure.Identity;

public class ApplicationUser : IdentityUser
{
    public string DisplayName { get; set; } = string.Empty;

    /// <summary>Deactivated accounts (U16) can't sign in — see AppSignInManager.CanSignInAsync — but are kept, not deleted, so history/audit attribution stays intact.</summary>
    public bool IsActive { get; set; } = true;

    /// <summary>Set on an account created/reset by an admin (temporary password or invite) — forces a password change on the next successful sign-in (U16).</summary>
    public bool MustChangePassword { get; set; }
}
