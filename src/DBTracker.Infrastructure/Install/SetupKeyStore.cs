using DBTracker.Core.Services.Install;

namespace DBTracker.Infrastructure.Install;

/// <summary>The one-time key that proves the person running the wizard can read files on the server.</summary>
public sealed class SetupKeyStore(string path)
{
    public string FilePath => path;

    /// <summary>Reuses a valid existing key so restarting the app mid-setup doesn't invalidate the one already copied.</summary>
    public string EnsureKey()
    {
        if (File.Exists(path))
        {
            var existing = SetupKey.Normalize(File.ReadAllText(path));
            if (existing.Length == SetupKey.Length)
            {
                return SetupKey.FormatForDisplay(existing);
            }
        }

        var key = SetupKey.Generate();
        Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(path))!);
        File.WriteAllText(path, key + Environment.NewLine);
        return key;
    }

    public bool Verify(string? candidate) => File.Exists(path) && SetupKey.Matches(File.ReadAllText(path), candidate);

    public void Delete()
    {
        if (File.Exists(path))
        {
            File.Delete(path);
        }
    }
}
