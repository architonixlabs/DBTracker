namespace DBTracker.Core.Services;

/// <summary>Role/scope check for change-window approvals. Admin can approve any scope; an unrecognized scope requires Admin.</summary>
/// <remarks>Core can't reference Infrastructure's Roles, so these literals must stay in sync with Roles.Admin/Dba/ItManager/Auditor.</remarks>
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

    /// <summary>The named scopes, in display order. Change-validation sign-offs use the same scopes and role rules.</summary>
    public static IReadOnlyList<string> KnownScopes { get; } = ["DBA", "Change manager", "Application owner", "Security", "Platform"];

    /// <summary>The known scopes these roles may approve or sign off.</summary>
    public static IReadOnlyList<string> AuthorizedScopes(IReadOnlyCollection<string> callerRoles) =>
        KnownScopes.Where(s => IsAuthorized(s, callerRoles)).ToList();

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
