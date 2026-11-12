using System;

namespace DBTracker.Tray.Core
{
    // The tray polls https://localhost:<port>, but the site's certificate is normally issued to the server's
    // real name, so a strict check would always fail on the name. A certificate that is in this computer's own
    // LocalMachine\My store is the one IIS is serving from this machine, so it is accepted; anything else needs
    // to pass the normal Windows checks.
    public static class TrayTls
    {
        public static bool Accept(bool passedWindowsChecks, string presentedThumbprint, Func<string, bool> isInMachineStore)
        {
            if (passedWindowsChecks)
            {
                return true;
            }
            if (string.IsNullOrWhiteSpace(presentedThumbprint) || isInMachineStore == null)
            {
                return false;
            }
            return isInMachineStore(presentedThumbprint.Trim().ToUpperInvariant());
        }
    }
}
