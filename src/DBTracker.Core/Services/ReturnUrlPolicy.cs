namespace DBTracker.Core.Services;

/// <summary>Where to send someone after they sign in. Only same-site paths pass, so a crafted returnUrl can't redirect off the site.</summary>
public static class ReturnUrlPolicy
{
    /// <summary>The path when it's local ("/x", not "//host" or "/\host"); otherwise "/".</summary>
    public static string LocalOrRoot(string? returnUrl) =>
        !string.IsNullOrEmpty(returnUrl)
        && returnUrl[0] == '/'
        && (returnUrl.Length == 1 || (returnUrl[1] != '/' && returnUrl[1] != '\\'))
        && !returnUrl.Any(char.IsControl)
            ? returnUrl
            : "/";
}
