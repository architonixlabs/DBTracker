using System.Diagnostics;
using System.Net;
using System.Net.Http;
using System.Text.Json;

namespace DBTracker.E2E;

/// <summary>Found by the Hyper-V upgrade test: after a reboot IIS can start DBTracker before SQL Server accepts
/// connections, and a failed start-up migration took the whole site down with HTTP 500. An installed app must instead
/// start, say it is waiting for its database, report degraded health and keep retrying.</summary>
/// <remarks>Runs a second copy of the built app in Production mode (the install feature on) against a SQL Server that
/// doesn't exist, on its own port and data folder, so the shared E2E server is untouched.</remarks>
[TestFixture]
public class DatabaseUnavailableAtStartupTests
{
    private const string Url = "http://localhost:5298";

    [Test]
    public async Task InstalledApp_WithNoDatabase_StartsAndSaysItIsWaiting_InsteadOfFailing()
    {
        var dataDir = Path.Combine(Path.GetTempPath(), $"dbtracker-e2e-nodb-{Guid.NewGuid():N}");
        Directory.CreateDirectory(dataDir);
        var config = new
        {
            ConnectionStrings = new { DBTracker = "Server=tcp:127.0.0.1,1;Database=DBTracker;Integrated Security=True;TrustServerCertificate=True;Connect Timeout=2" },
            Install = new { CompletedAtUtc = DateTime.UtcNow.ToString("O"), CompletedBy = "e2e" },
        };
        await File.WriteAllTextAsync(Path.Combine(dataDir, "dbtracker.config.json"), JsonSerializer.Serialize(config));

        var webDir = FindWebProjectDirectory();
        var dll = Directory.GetFiles(Path.Combine(webDir, "bin"), "DBTracker.Web.dll", SearchOption.AllDirectories)
            .Where(f => f.Contains($"{Path.DirectorySeparatorChar}Debug{Path.DirectorySeparatorChar}"))
            .OrderByDescending(File.GetLastWriteTimeUtc).First();

        using var process = new Process
        {
            StartInfo = new ProcessStartInfo("dotnet", $"\"{dll}\"")
            {
                WorkingDirectory = webDir,
                UseShellExecute = false,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                CreateNoWindow = true,
            },
        };
        process.StartInfo.EnvironmentVariables["ASPNETCORE_ENVIRONMENT"] = "Production";
        process.StartInfo.EnvironmentVariables["ASPNETCORE_URLS"] = Url;
        process.StartInfo.EnvironmentVariables["DBTRACKER_DATA_DIR"] = dataDir;
        var output = new System.Text.StringBuilder();
        process.OutputDataReceived += (_, e) => { if (e.Data is not null) lock (output) output.AppendLine(e.Data); };
        process.ErrorDataReceived += (_, e) => { if (e.Data is not null) lock (output) output.AppendLine(e.Data); };
        process.Start();
        process.BeginOutputReadLine();
        process.BeginErrorReadLine();

        try
        {
            using var http = new HttpClient { Timeout = TimeSpan.FromSeconds(10) };
            HttpResponseMessage? page = null;
            var deadline = DateTime.UtcNow.AddSeconds(90);
            while (DateTime.UtcNow < deadline && !process.HasExited)
            {
                try
                {
                    page = await http.GetAsync(Url + "/");
                    break;
                }
                catch (HttpRequestException)
                {
                    await Task.Delay(1000); // still in its start-up retries
                }
            }

            Assert.That(process.HasExited, Is.False, "The app must not exit when the database is unreachable.\n" + output);
            Assert.That(page, Is.Not.Null, "The app never started listening.\n" + output);
            Assert.That(page!.StatusCode, Is.EqualTo(HttpStatusCode.ServiceUnavailable));
            Assert.That(page.Headers.RetryAfter?.Delta, Is.EqualTo(TimeSpan.FromSeconds(15)));
            var body = await page.Content.ReadAsStringAsync();
            Assert.That(body, Does.Contain("waiting for its database"));

            var health = await http.GetAsync(Url + "/healthz");
            Assert.That(health.StatusCode, Is.EqualTo(HttpStatusCode.OK));
            var json = await health.Content.ReadAsStringAsync();
            Assert.That(json, Does.Contain("degraded"));
        }
        finally
        {
            if (!process.HasExited)
            {
                process.Kill(entireProcessTree: true);
                await process.WaitForExitAsync();
            }
            try { Directory.Delete(dataDir, recursive: true); } catch (IOException) { }
        }
    }

    private static string FindWebProjectDirectory()
    {
        var current = new DirectoryInfo(AppContext.BaseDirectory);
        while (current is not null && !Directory.Exists(Path.Combine(current.FullName, "src", "DBTracker.Web")))
        {
            current = current.Parent;
        }
        return Path.Combine(current!.FullName, "src", "DBTracker.Web");
    }
}
