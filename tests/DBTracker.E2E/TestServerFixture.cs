using System.Diagnostics;
using System.Net.Http;
using System.Text;
using Microsoft.Playwright;

namespace DBTracker.E2E;

/// <summary>
/// Launches the real DBTracker.Web app as a child process against the DBTracker_Test database
/// (Testing environment — seeded with the same demo data as Development) and drives it with a
/// real Chromium browser via Playwright. Blazor Server needs live WebSocket/SignalR, so this runs
/// an actual Kestrel process rather than an in-memory TestServer. Launches the built DLL directly
/// (not `dotnet run`) so there's exactly one process to track and no MSBuild/run-command overhead.
/// </summary>
public class TestServerFixture : IAsyncDisposable
{
    public const string BaseUrl = "http://localhost:5299";

    private Process? _process;
    private readonly StringBuilder _output = new();
    public IPlaywright Playwright { get; private set; } = null!;
    public IBrowser Browser { get; private set; } = null!;

    /// <summary>Captured stdout/stderr of the server process, for diagnosing a test failure that isn't visible from the browser side alone.</summary>
    public string CapturedOutput { get { lock (_output) return _output.ToString(); } }

    public async Task InitializeAsync()
    {
        var webProjectDir = FindWebProjectDirectory();
        var dllPath = FindWebDll(webProjectDir);

        _process = new Process
        {
            StartInfo = new ProcessStartInfo
            {
                FileName = "dotnet",
                Arguments = $"\"{dllPath}\"",
                WorkingDirectory = webProjectDir,
                UseShellExecute = false,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                CreateNoWindow = true,
            },
            EnableRaisingEvents = true,
        };
        _process.StartInfo.EnvironmentVariables["ASPNETCORE_ENVIRONMENT"] = "Testing";
        _process.StartInfo.EnvironmentVariables["ASPNETCORE_URLS"] = BaseUrl;

        _process.OutputDataReceived += (_, e) => { if (e.Data is not null) lock (_output) _output.AppendLine(e.Data); };
        _process.ErrorDataReceived += (_, e) => { if (e.Data is not null) lock (_output) _output.AppendLine("[err] " + e.Data); };

        _process.Start();
        _process.BeginOutputReadLine();
        _process.BeginErrorReadLine();

        await WaitForServerReadyAsync(_process);

        Playwright = await Microsoft.Playwright.Playwright.CreateAsync();
        Browser = await Playwright.Chromium.LaunchAsync(new BrowserTypeLaunchOptions
        {
            Headless = true
        });
    }

    private static string FindWebProjectDirectory()
    {
        var dir = AppContext.BaseDirectory;
        var current = new DirectoryInfo(dir);
        while (current is not null && !Directory.Exists(Path.Combine(current.FullName, "src", "DBTracker.Web")))
        {
            current = current.Parent;
        }

        if (current is null)
        {
            throw new InvalidOperationException("Could not locate src/DBTracker.Web relative to test output.");
        }

        return Path.Combine(current.FullName, "src", "DBTracker.Web");
    }

    private static string FindWebDll(string webProjectDir)
    {
        var path = Path.Combine(webProjectDir, "bin", "Debug", "net10.0", "DBTracker.Web.dll");
        if (!File.Exists(path))
        {
            throw new FileNotFoundException(
                $"DBTracker.Web.dll not found at '{path}'. Build the solution before running E2E tests.", path);
        }
        return path;
    }

    private async Task WaitForServerReadyAsync(Process process)
    {
        using var client = new HttpClient();
        var deadline = DateTime.UtcNow.AddSeconds(60);

        while (DateTime.UtcNow < deadline)
        {
            if (process.HasExited)
            {
                throw new InvalidOperationException(
                    $"DBTracker.Web exited early with code {process.ExitCode}.\n--- output ---\n{_output}");
            }

            try
            {
                var response = await client.GetAsync(BaseUrl + "/");
                if (response.IsSuccessStatusCode)
                {
                    return;
                }
            }
            catch
            {
                // server not up yet
            }

            await Task.Delay(500);
        }

        throw new TimeoutException($"DBTracker.Web did not become ready within 60 seconds.\n--- output ---\n{_output}");
    }

    public async ValueTask DisposeAsync()
    {
        if (Browser is not null)
        {
            await Browser.CloseAsync();
        }
        Playwright?.Dispose();

        // Always dump the server's captured stdout/stderr, not just on a startup timeout — a
        // mid-run crash (e.g. an unhandled exception that brings the Kestrel process down) shows
        // up to Playwright only as ERR_CONNECTION_REFUSED on every subsequent test, with no clue
        // why. This file is overwritten every run, so it always reflects the most recent one.
        try
        {
            await File.WriteAllTextAsync(Path.Combine(AppContext.BaseDirectory, "e2e-server-output.log"), CapturedOutput);
        }
        catch
        {
            // best-effort diagnostic only
        }

        if (_process is { HasExited: false })
        {
            try
            {
                _process.Kill(entireProcessTree: true);
            }
            catch
            {
                // already exited
            }
        }
        _process?.Dispose();
    }
}
