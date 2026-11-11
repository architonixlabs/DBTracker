using System;
using System.Linq;
using System.Security.Principal;
using System.Threading;
using System.Windows.Forms;
using DBTracker.Tray.Core;

namespace DBTracker.Tray
{
    internal static class Program
    {
        public const string AutostartSwitch = "--autostart";

        [STAThread]
        private static int Main(string[] args)
        {
            var adminIndex = Array.FindIndex(args, a => string.Equals(a, AdminCommands.Switch, StringComparison.OrdinalIgnoreCase));
            if (adminIndex >= 0)
            {
                var action = adminIndex + 1 < args.Length ? args[adminIndex + 1] : null;
                var resultFile = ArgumentAfter(args, "--result");
                return AdminRunner.Run(action, resultFile);
            }

            // The machine-wide Run entry starts every user's copy; a user can opt out from the menu.
            if (args.Any(a => string.Equals(a, AutostartSwitch, StringComparison.OrdinalIgnoreCase)) && !UserPreferences.StartWithWindows)
            {
                return 0;
            }

            var sid = WindowsIdentity.GetCurrent().User?.Value ?? Environment.UserName;
            using (var mutex = new Mutex(true, @"Local\DBTrackerTray-" + sid, out var firstInstance))
            {
                if (!firstInstance)
                {
                    return 0;
                }

                Application.EnableVisualStyles();
                Application.SetCompatibleTextRenderingDefault(false);
                Application.Run(new TrayContext(RegistrySettings.Load()));
                GC.KeepAlive(mutex);
            }
            return 0;
        }

        private static string ArgumentAfter(string[] args, string name)
        {
            var i = Array.FindIndex(args, a => string.Equals(a, name, StringComparison.OrdinalIgnoreCase));
            return i >= 0 && i + 1 < args.Length ? args[i + 1] : null;
        }
    }
}
