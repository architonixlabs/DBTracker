namespace DBTracker.Core.Services.Install;

public sealed record InstallDataPaths(string DataDirectory)
{
    public const string DataDirectoryEnvironmentVariable = "DBTRACKER_DATA_DIR";
    public const string ConfigFileName = "dbtracker.config.json";
    public const string SetupKeyFileName = "setup-key.txt";

    public string ConfigFile => Path.Combine(DataDirectory, ConfigFileName);
    public string SetupKeyFile => Path.Combine(DataDirectory, SetupKeyFileName);
    public string KeysDirectory => Path.Combine(DataDirectory, "keys");

    public static InstallDataPaths Resolve(string contentRoot, string? dataDirectoryOverride) =>
        new(string.IsNullOrWhiteSpace(dataDirectoryOverride)
            ? Path.Combine(contentRoot, "App_Data")
            : Path.GetFullPath(dataDirectoryOverride));
}
