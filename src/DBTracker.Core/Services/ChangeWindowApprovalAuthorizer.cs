namespace DBTracker.Core.Services;

/// <summary>
/// Pure role/scope authorization for D12 change-window approvals. Deliberately references role
/// names as literal strings rather than DBTracker.Infrastructure.Identity.Roles, since
/// DBTracker.Core cannot depend on DBTracker.Infrastructure — the literal values here must stay in
/// sync with Roles.Admin/Dba/ItManager/Auditor if those ever change. Admin can always approve any
/// scope; an unrecognized scope label requires Admin specifically rather than defaulting open.
/// </summary>
public static class ChangeWindowApprovalAuthorizer
{
    private static readonly Dictionary<string, string[]> ScopeAllowedRoles = new(StringComparer.OrdinalIgnoreCase)
    {
        ["DBA"] = ["Admin", "DBA"],
        ["Change manager"] = ["Admin", "ITManager"],
        ["Application owner"] = ["Admin", "ITManager", "DBA"],
        ["Security"] = ["Admin", "Auditor"],
        ["Platform"] = ["Admin", "ITManager", "DBA"],
    };

    public static bool IsAuthorized(string requiredScope, IReadOnlyCollection<string> callerRoles)
    {
        if (callerRoles.Contains("Admin"))
        {
            return true;
        }

        var trimmed = requiredScope.Trim();
        if (ScopeAllowedRoles.TryGetValue(trimmed, out var allowed))
        {
            return allowed.Any(callerRoles.Contains);
        }

        // An unrecognized/custom scope label is conservative: Admin-only (already checked above).
        return false;
    }
}
