using System.Diagnostics;
using System.Text;
using Microsoft.Data.SqlClient;
using Microsoft.Playwright;

namespace DBTracker.InstallE2E;

/// <summary>
/// Starts DBTracker the way a fresh IIS deployment would: Production environment, no connection string, an empty data folder.
/// Uses its own port, data folder and throwaway database name so it never touches the shared Testing server or DBTracker_Test,
/// and drops that database on dispose.
/// </summary>
public sealed class InstallServerFixture : IAsyncDisposable
{
    public const string BaseUrl = "http://localhost:5313";

    private Process? _process;
    private readonly StringBuilder _output = new();

    public string DataDirectory { get; } = Path.Combine(Path.GetTempPath(), "dbt-install-e2e-" + Guid.NewGuid().ToString("N"));
    public string DatabaseName { get; } = "DBTracker_InstallTest_" + Guid.NewGuid().ToString("N");
    public IPlaywright Playwright { get; private set; } = null!;
    public IBrowser Browser { get; private set; } = null!;

    public string CapturedOutput { get { lock (_output) return _output.ToString(); } }

    public string ReadSetupKey() => File.ReadAllText(Path.Combine(DataDirectory, "setup-key.txt")).Trim();

    public async Task InitializeAsync()
    {
        Directory.CreateDirectory(DataDirectory);
        await StartProcessAsync();

        Playwright = await Microsoft.Playwright.Playwright.CreateAsync();
        Browser = await Playwright.Chromium.LaunchAsync(new BrowserTypeLaunchOptions
        {
            Headless = Environment.GetEnvironmentVariable("E2E_HEADED") != "1"
        });
    }

    /// <summary>Stops and starts the app on the same data folder, the way an IIS recycle would.</summary>
    public async Task RestartAsync()
    {
        await StopProcessAsync();
        await StartProcessAsync();
    }

    private async Task StopProcessAsync()
    {
        if (_process is { HasExited: false })
        {
            _process.Kill(entireProcessTree: true);
            await _process.WaitForExitAsync();
        }
        _process?.Dispose();
        _process = null;
    }

    private async Task StartProcessAsync()
    {
        var webProjectDir = FindWebProjectDirectory();
        var dllPath = Path.Combine(webProjectDir, "bin", "Debug", "net10.0", "DBTracker.Web.dll");
        if (!File.Exists(dllPath))
        {
            throw new FileNotFoundException("Build the solution before running E2E tests.", dllPath);
        }

        var start = new ProcessStartInfo
        {
            FileName = "dotnet",
            Arguments = $"\"{dllPath}\"",
            WorkingDirectory = webProjectDir,
            UseShellExecute = false,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            CreateNoWindow = true,
        };
        start.EnvironmentVariables["ASPNETCORE_ENVIRONMENT"] = "Production";
        start.EnvironmentVariables["ASPNETCORE_URLS"] = BaseUrl;
        start.EnvironmentVariables["DBTRACKER_DATA_DIR"] = DataDirectory;
        start.EnvironmentVariables.Remove("ConnectionStrings__DBTracker");

        _process = new Process { StartInfo = start, EnableRaisingEvents = true };
        _process.OutputDataReceived += (_, e) => { if (e.Data is not null) lock (_output) _output.AppendLine(e.Data); };
        _process.ErrorDataReceived += (_, e) => { if (e.Data is not null) lock (_output) _output.AppendLine("[err] " + e.Data); };
        _process.Start();
        _process.BeginOutputReadLine();
        _process.BeginErrorReadLine();

        await WaitForServerReadyAsync();
    }

    private async Task WaitForServerReadyAsync()
    {
        // Login answers 200 whether or not setup is done (before setup it redirects to /install, which also answers 200).
        using var client = new HttpClient();
        var deadline = DateTime.UtcNow.AddSeconds(60);
        while (DateTime.UtcNow < deadline)
        {
            if (_process!.HasExited)
            {
                throw new InvalidOperationException($"DBTracker.Web exited early with code {_process.ExitCode}.\n{CapturedOutput}");
            }

            try
            {
                if ((await client.GetAsync(BaseUrl + "/Account/Login")).IsSuccessStatusCode)
                {
                    return;
                }
            }
            catch (HttpRequestException)
            {
                // not listening yet
            }
            await Task.Delay(500);
        }
        throw new TimeoutException($"DBTracker.Web did not start within 60 seconds.\n{CapturedOutput}");
    }

    private static string FindWebProjectDirectory()
    {
        var current = new DirectoryInfo(AppContext.BaseDirectory);
        while (current is not null && !Directory.Exists(Path.Combine(current.FullName, "src", "DBTracker.Web")))
        {
            current = current.Parent;
        }
        return current is null
            ? throw new InvalidOperationException("Could not locate src/DBTracker.Web relative to test output.")
            : Path.Combine(current.FullName, "src", "DBTracker.Web");
    }

    public async ValueTask DisposeAsync()
    {
        if (Browser is not null)
        {
            await Browser.CloseAsync();
        }
        Playwright?.Dispose();

        try
        {
            await File.WriteAllTextAsync(Path.Combine(AppContext.BaseDirectory, "e2e-install-server-output.log"), CapturedOutput);
        }
        catch (IOException)
        {
            // diagnostic only
        }

        await StopProcessAsync();
        await DropDatabaseAsync();

        if (Directory.Exists(DataDirectory))
        {
            Directory.Delete(DataDirectory, recursive: true);
        }
    }

    private async Task DropDatabaseAsync()
    {
        SqlConnection.ClearAllPools();
        await using var conn = new SqlConnection("Server=localhost;Database=master;Integrated Security=True;TrustServerCertificate=True;");
        await conn.OpenAsync();
        await using var cmd = new SqlCommand(
            "IF DB_ID(@name) IS NOT NULL BEGIN " +
            "DECLARE @sql nvarchar(400) = N'ALTER DATABASE ' + QUOTENAME(@name) + N' SET SINGLE_USER WITH ROLLBACK IMMEDIATE; DROP DATABASE ' + QUOTENAME(@name); " +
            "EXEC (@sql); END", conn);
        cmd.Parameters.AddWithValue("@name", DatabaseName);
        await cmd.ExecuteNonQueryAsync();
    }
}
