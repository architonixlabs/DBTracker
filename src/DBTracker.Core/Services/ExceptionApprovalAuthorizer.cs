namespace DBTracker.Core.Services;

/// <summary>
/// Pure role check for who may approve/reject/revoke a D14 ReadinessException. An empty
/// PermittedApproverRole means Admin-only; a non-empty value requires that exact role (literal
/// string, same caveat as ChangeWindowApprovalAuthorizer — DBTracker.Core cannot reference
/// DBTracker.Infrastructure.Identity.Roles) or Admin.
/// </summary>
public static class ExceptionApprovalAuthorizer
{
    public static bool IsAuthorized(string permittedApproverRole, IReadOnlyCollection<string> callerRoles)
    {
        if (callerRoles.Contains("Admin"))
        {
            return true;
        }

        return !string.IsNullOrWhiteSpace(permittedApproverRole) && callerRoles.Contains(permittedApproverRole.Trim());
    }
}
