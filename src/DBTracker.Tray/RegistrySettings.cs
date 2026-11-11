using System;
using Microsoft.Win32;
using DBTracker.Tray.Core;

namespace DBTracker.Tray
{
    // Reads what DBTracker.msi records under HKLM\SOFTWARE\Architonix Labs\DBTracker (64-bit view).
    internal static class RegistrySettings
    {
        public static TraySettings Load()
        {
            try
            {
                using (var hklm = RegistryKey.OpenBaseKey(RegistryHive.LocalMachine, RegistryView.Registry64))
                using (var key = hklm.OpenSubKey(@"SOFTWARE\Architonix Labs\DBTracker"))
                {
                    if (key == null)
                    {
                        return TraySettings.From(null, null, null, null);
                    }
                    return TraySettings.From(
                        key.GetValue("SitePort") as string,
                        key.GetValue("InstallFolder") as string,
                        key.GetValue("SiteName") as string,
                        key.GetValue("AppPoolName") as string);
                }
            }
            catch (Exception ex) when (ex is System.Security.SecurityException || ex is UnauthorizedAccessException)
            {
                return TraySettings.From(null, null, null, null);
            }
        }

        public static string DataDirOverride() =>
            Environment.GetEnvironmentVariable("DBTRACKER_DATA_DIR", EnvironmentVariableTarget.Machine);
    }

    // Per-user choices, under HKCU so each Windows user decides for themselves.
    internal static class UserPreferences
    {
        private const string KeyPath = @"Software\Architonix Labs\DBTracker Tray";

        public static bool StartWithWindows
        {
            get
            {
                using (var key = Registry.CurrentUser.OpenSubKey(KeyPath))
                {
                    return !(key?.GetValue("StartWithWindows") is int value) || value != 0;
                }
            }
            set
            {
                using (var key = Registry.CurrentUser.CreateSubKey(KeyPath))
                {
                    key.SetValue("StartWithWindows", value ? 1 : 0, RegistryValueKind.DWord);
                }
            }
        }
    }
}
