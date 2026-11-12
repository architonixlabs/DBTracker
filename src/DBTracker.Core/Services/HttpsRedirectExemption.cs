namespace DBTracker.Core.Services;

/// <summary>Which requests skip the HTTP-to-HTTPS redirect: only the tray's own endpoints, and only from this machine.</summary>
/// <remarks>The tray polls http://localhost:&lt;port&gt; and doesn't follow redirects, so once an HTTPS binding is added a
/// redirect would turn its health light red and drop its token. Loopback traffic never leaves the server, so the token
/// stays off the network; every other request, including the tray endpoints from another machine, is still redirected.</remarks>
public static class HttpsRedirectExemption
{
    private static readonly string[] TrayPaths = ["/healthz", "/api/tray"];

    public static bool Applies(string? path, bool isLoopback)
    {
        if (!isLoopback || string.IsNullOrEmpty(path))
        {
            return false;
        }

        return TrayPaths.Any(p => path.Equals(p, StringComparison.OrdinalIgnoreCase)
            || path.StartsWith(p + "/", StringComparison.OrdinalIgnoreCase));
    }
}
