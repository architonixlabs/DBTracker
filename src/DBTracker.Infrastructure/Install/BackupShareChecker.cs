using System.Security.Principal;

namespace DBTracker.Infrastructure.Install;

public sealed record BackupShareCheckResult(bool CanList, string Identity, string Message);

public static class BackupShareChecker
{
    public static string CurrentIdentityName() =>
        OperatingSystem.IsWindows() ? WindowsIdentity.GetCurrent().Name : Environment.UserName;

    /// <summary>Lists the folder as the account the app runs under. Unreachable UNC paths can hang, so the probe is time-boxed.</summary>
    public static async Task<BackupShareCheckResult> CheckAsync(string path, TimeSpan timeout)
    {
        var identity = CurrentIdentityName();
        if (string.IsNullOrWhiteSpace(path))
        {
            return new BackupShareCheckResult(false, identity, "Enter a folder or share path to check.");
        }

        var probe = Task.Run(() =>
        {
            try
            {
                if (!Directory.Exists(path))
                {
                    return new BackupShareCheckResult(false, identity,
                        $"{identity} can't see {path}. Either it doesn't exist or this account has no permission on the share or folder.");
                }

                var entries = Directory.EnumerateFileSystemEntries(path).Take(1000).Count();
                return new BackupShareCheckResult(true, identity,
                    entries == 0
                        ? $"{identity} can open {path}. It's empty right now."
                        : $"{identity} can open {path} and list its contents.");
            }
            catch (UnauthorizedAccessException)
            {
                return new BackupShareCheckResult(false, identity, $"{identity} was refused access to {path}. Grant it read permission on the share and the folder.");
            }
            catch (IOException ex)
            {
                return new BackupShareCheckResult(false, identity, $"Couldn't read {path}: {ex.Message}");
            }
        });

        var finished = await Task.WhenAny(probe, Task.Delay(timeout));
        return finished == probe
            ? await probe
            : new BackupShareCheckResult(false, identity, $"No answer from {path} after {timeout.TotalSeconds:N0} seconds. Check the server name and that the share is reachable from this machine.");
    }
}
