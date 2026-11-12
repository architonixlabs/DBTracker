using DBTracker.Core.Services.Install;
using DBTracker.Infrastructure;
using DBTracker.Infrastructure.Identity;
using DBTracker.Infrastructure.Install;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration.Json;

namespace DBTracker.Web.Install;

public static class InstallStartupExtensions
{
    /// <summary>
    /// Loads App_Data/dbtracker.config.json (decrypting its secrets), persists Data Protection keys next to it, decides whether the
    /// app is installed, and registers the wizard's services. Development and Testing skip all of it and keep configuring the
    /// database from appsettings.
    /// </summary>
    public static async Task<InstallState> AddInstallSupportAsync(this WebApplicationBuilder builder)
    {
        var featureEnabled = !(builder.Environment.IsDevelopment() || builder.Environment.IsEnvironment("Testing"));
        var paths = InstallDataPaths.Resolve(builder.Environment.ContentRootPath,
            Environment.GetEnvironmentVariable(InstallDataPaths.DataDirectoryEnvironmentVariable));

        if (featureEnabled)
        {
            AddConfigFileAndKeys(builder, paths);
        }

        var connectionString = builder.Configuration.GetConnectionString("DBTracker");
        if (!featureEnabled && string.IsNullOrWhiteSpace(connectionString))
        {
            throw new InvalidOperationException("Connection string 'DBTracker' not configured.");
        }

        var installed = !featureEnabled || await IsAlreadyInstalledAsync(builder.Configuration, connectionString);
        var state = new InstallState(builder.Configuration, featureEnabled, installed);

        builder.Services.AddSingleton(state);
        builder.Services.AddSingleton<DatabaseReadiness>();
        builder.Services.AddHostedService<DatabaseReadinessService>();
        builder.Services.AddSingleton(paths);
        builder.Services.AddSingleton(new SetupKeyStore(paths.SetupKeyFile));
        builder.Services.AddSingleton(new AttemptLimiter(maxFailures: 5, window: TimeSpan.FromMinutes(1)));
        builder.Services.AddSingleton<InstallDatabaseService>();
        builder.Services.AddSingleton<InstallWizardService>();
        return state;
    }

    /// <summary>Migrates (and in Development/Testing seeds) an installed app; otherwise makes sure a setup key exists and logs it.</summary>
    public static async Task PrepareDatabaseAsync(this WebApplication app, InstallState state)
    {
        if (!state.IsInstalled)
        {
            var store = app.Services.GetRequiredService<SetupKeyStore>();
            app.Logger.LogWarning(
                "DBTracker isn't set up yet. Open /install in a browser and enter the setup key {SetupKey}. It is also saved in {SetupKeyFile}.",
                store.EnsureKey(), store.FilePath);
            return;
        }

        // A few quick tries cover SQL Server finishing its start-up; after that the site opens in a waiting state instead of
        // crashing, and DatabaseReadinessService keeps retrying (see DatabaseReadiness).
        var readiness = app.Services.GetRequiredService<DatabaseReadiness>();
        for (var attempt = 1; attempt <= 4; attempt++)
        {
            try
            {
                await DatabaseReadiness.MigrateAsync(app.Services, state, CancellationToken.None);
                readiness.MarkReady();
                return;
            }
            catch (Exception ex)
            {
                readiness.MarkFailed(ex);
                app.Logger.LogWarning("Can't reach the database yet (attempt {Attempt} of 4): {Error}", attempt, readiness.LastError);
                if (attempt < 4)
                {
                    await Task.Delay(DBTracker.Core.Services.DatabaseReadinessGate.StartupBackoff(attempt));
                }
            }
        }

        // Testing and Development need a working database for every test and page; fail loudly there.
        if (!state.InstallFeatureEnabled)
        {
            throw new InvalidOperationException("The database isn't reachable: " + readiness.LastError);
        }
        app.Logger.LogError("DBTracker started without its database ({Error}). Pages say so and the app retries every {Seconds} seconds.",
            readiness.LastError, DBTracker.Core.Services.DatabaseReadinessGate.RetryAfterSeconds);
    }

    /// <summary>Before setup, every page goes to the wizard; afterwards the wizard is gone (404).</summary>
    public static IApplicationBuilder UseInstallGate(this IApplicationBuilder app, InstallState state) =>
        app.Use(async (context, next) =>
        {
            var path = context.Request.Path;
            var isInstallPath = path.StartsWithSegments("/install", StringComparison.OrdinalIgnoreCase);

            if (state.IsInstalled && isInstallPath)
            {
                // Answered here: the app's own not-found page requires sign-in, so re-executing would turn this into a login redirect.
                if (context.Features.Get<Microsoft.AspNetCore.Diagnostics.IStatusCodePagesFeature>() is { } statusPages)
                {
                    statusPages.Enabled = false;
                }
                context.Response.StatusCode = StatusCodes.Status404NotFound;
                context.Response.ContentType = "text/html; charset=utf-8";
                await context.Response.WriteAsync(
                    "<!DOCTYPE html><html lang=\"en\"><head><meta charset=\"utf-8\"><title>Not found · DBTracker</title></head>" +
                    "<body><h1>Not found</h1><p>DBTracker is already set up. <a href=\"/\">Go to DBTracker</a>.</p></body></html>");
                return;
            }

            if (state.IsInstalled || isInstallPath || IsFrameworkOrAsset(path) || path.Equals("/healthz", StringComparison.OrdinalIgnoreCase))
            {
                await next();
                return;
            }

            // API clients (the tray app) expect JSON, not the wizard's HTML.
            if (path.StartsWithSegments("/api", StringComparison.OrdinalIgnoreCase))
            {
                context.Response.StatusCode = StatusCodes.Status503ServiceUnavailable;
                await context.Response.WriteAsJsonAsync(new { error = "setup_required", message = "DBTracker isn't set up yet." });
                return;
            }

            context.Response.Redirect("/install");
        });

    private static bool IsFrameworkOrAsset(PathString path) =>
        path.StartsWithSegments("/_framework")
        || path.StartsWithSegments("/_blazor")
        || path.StartsWithSegments("/_content")
        || path.StartsWithSegments("/Error")
        || path.StartsWithSegments("/not-found")
        || Path.HasExtension(path.Value);

    private static void AddConfigFileAndKeys(WebApplicationBuilder builder, InstallDataPaths paths)
    {
        Directory.CreateDirectory(paths.DataDirectory);

        var protector = DataProtectionProvider
            .Create(new DirectoryInfo(paths.KeysDirectory), ConfigureKeys)
            .CreateProtector(InstallConfigWriter.ProtectorPurpose);

        // Right after appsettings*.json, so the wizard's settings override them but environment variables still win.
        var sources = builder.Configuration.Sources;
        var insertAt = 0;
        for (var i = 0; i < sources.Count; i++)
        {
            if (sources[i] is JsonConfigurationSource json && json.Path?.StartsWith("appsettings", StringComparison.OrdinalIgnoreCase) == true)
            {
                insertAt = i + 1;
            }
        }
        builder.Configuration.AddProtectedJsonFile(paths.ConfigFile, protector, insertAt);

        ConfigureKeys(builder.Services.AddDataProtection().PersistKeysToFileSystem(new DirectoryInfo(paths.KeysDirectory)));
    }

    private static void ConfigureKeys(IDataProtectionBuilder dataProtection)
    {
        dataProtection.SetApplicationName("DBTracker");
        if (OperatingSystem.IsWindows())
        {
            // Machine scope: IIS app pool identities often run without a loaded user profile.
            dataProtection.ProtectKeysWithDpapi(protectToLocalMachine: true);
        }
    }

    private static async Task<bool> IsAlreadyInstalledAsync(IConfiguration configuration, string? connectionString)
    {
        var hasMarker = !string.IsNullOrWhiteSpace(configuration["Install:CompletedAtUtc"]);
        var hasConnectionString = !string.IsNullOrWhiteSpace(connectionString);
        var probe = !hasMarker && hasConnectionString
            ? await InstallDatabaseService.ProbeForAdminAsync(connectionString!)
            : InstallProbeResult.NotProbed;
        return InstallStateResolver.IsInstalled(true, hasMarker, hasConnectionString, probe);
    }
}
