namespace DBTracker.Core.Services.Install;

/// <summary>What probing an already-configured connection string found at startup.</summary>
public enum InstallProbeResult
{
    NotProbed,
    AdminExists,
    NoAdminOrDatabase,
    Unreachable
}

public static class InstallStateResolver
{
    /// <summary>
    /// Decides whether the app starts in normal mode or sends everyone to the install wizard.
    /// An unreachable database counts as installed: a deployment whose SQL Server is briefly down at
    /// boot should keep failing the way it always did, not reopen setup.
    /// </summary>
    public static bool IsInstalled(bool installFeatureEnabled, bool hasCompletedMarker, bool hasConnectionString, InstallProbeResult probe)
    {
        if (!installFeatureEnabled || hasCompletedMarker)
        {
            return true;
        }

        if (!hasConnectionString)
        {
            return false;
        }

        return probe is InstallProbeResult.AdminExists or InstallProbeResult.Unreachable;
    }
}
