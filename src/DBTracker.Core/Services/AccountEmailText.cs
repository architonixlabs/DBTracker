namespace DBTracker.Core.Services;

/// <summary>Account email bodies. The invite carries a set-password link and never a password.</summary>
public static class AccountEmailText
{
    public const string InviteSubject = "Your DBTracker account";

    public static string InviteBody(string setPasswordLink, string signInEmail) =>
        "An account was created for you on DBTracker.\n\n" +
        $"Set your password here (single use, expires in 24 hours): {setPasswordLink}\n\n" +
        $"Your sign-in email is {signInEmail}. If the link has expired, use \"Forgot password\" on the sign-in page.";
}
