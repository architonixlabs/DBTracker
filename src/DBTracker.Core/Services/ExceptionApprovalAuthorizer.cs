namespace DBTracker.Core.Services;

/// <summary>Who may approve, reject or revoke a readiness exception. Empty PermittedApproverRole means Admin-only; otherwise that exact role or Admin. Role names are literals (see ChangeWindowApprovalAuthorizer).</summary>
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
