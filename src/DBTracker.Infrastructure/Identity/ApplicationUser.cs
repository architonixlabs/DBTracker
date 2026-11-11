using Microsoft.AspNetCore.Identity;

namespace DBTracker.Infrastructure.Identity;

public class ApplicationUser : IdentityUser
{
    public string DisplayName { get; set; } = string.Empty;

    /// <summary>Deactivated accounts can't sign in but are kept, so audit attribution stays intact.</summary>
    public bool IsActive { get; set; } = true;

    /// <summary>Set when an admin creates or resets the account; forces a password change at next sign-in.</summary>
    public bool MustChangePassword { get; set; }
}
