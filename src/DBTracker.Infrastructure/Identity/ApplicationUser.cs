using Microsoft.AspNetCore.Identity;

namespace DBTracker.Infrastructure.Identity;

public class ApplicationUser : IdentityUser
{
    public string DisplayName { get; set; } = string.Empty;
}
