using DBTracker.Infrastructure.Install;
using DBTracker.Core.Services;
using DBTracker.Infrastructure;
using DBTracker.Infrastructure.Identity;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

namespace DBTracker.Web.Install;

/// <summary>Whether an installed DBTracker has reached its database and applied its migrations.</summary>
/// <remarks>Found by the Hyper-V upgrade test: after a reboot IIS can start the app before SQL Server accepts connections.
/// Migrating once and failing took the whole site down with HTTP 500 until someone recycled it. Now start-up retries
/// briefly, then the site answers 503 with an explanation while <see cref="DatabaseReadinessService"/> keeps retrying.</remarks>
public sealed class DatabaseReadiness
{
    private volatile bool _waiting;

    /// <summary>True only after an installed app failed to reach its database, until a retry succeeds. A fresh install never
    /// waits: the setup wizard creates and migrates the database itself before it marks the app installed.</summary>
    public bool IsWaiting => _waiting;
    public string? LastError { get; private set; }
    public DateTime? LastAttemptUtc { get; private set; }

    internal void MarkReady() { _waiting = false; LastError = null; }

    internal void MarkFailed(Exception error)
    {
        _waiting = true;
        LastAttemptUtc = DateTime.UtcNow;
        LastError = error.GetBaseException().Message;
    }

    /// <summary>Applies pending migrations and, in Development/Testing, the seed data. Throws when the database can't be reached.</summary>
    internal static async Task MigrateAsync(IServiceProvider services, InstallState state, CancellationToken ct)
    {
        using var scope = services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<DBTrackerDbContext>();
        await db.Database.MigrateAsync(ct);

        if (!state.InstallFeatureEnabled)
        {
            await SeedData.SeedAsync(db);
            await IdentitySeedData.SeedAsync(
                scope.ServiceProvider.GetRequiredService<RoleManager<IdentityRole>>(),
                scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>());
        }
    }
}

/// <summary>Retries the migration every 15 seconds until the database answers, then opens the site.</summary>
public sealed class DatabaseReadinessService(
    DatabaseReadiness readiness,
    InstallState state,
    IServiceProvider services,
    ILogger<DatabaseReadinessService> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (readiness.IsWaiting && !stoppingToken.IsCancellationRequested)
        {
            await Task.Delay(TimeSpan.FromSeconds(DatabaseReadinessGate.RetryAfterSeconds), stoppingToken);
            try
            {
                await DatabaseReadiness.MigrateAsync(services, state, stoppingToken);
                readiness.MarkReady();
                logger.LogInformation("The database is reachable again and its migrations are applied. DBTracker is open.");
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                readiness.MarkFailed(ex);
                logger.LogWarning("The database still can't be reached ({Error}). Trying again in {Seconds} seconds.", readiness.LastError, DatabaseReadinessGate.RetryAfterSeconds);
            }
        }
    }
}

public static class DatabaseReadinessExtensions
{
    /// <summary>Answers 503 with a plain explanation while an installed app is waiting for its database.</summary>
    public static IApplicationBuilder UseDatabaseReadinessGate(this IApplicationBuilder app, InstallState state, DatabaseReadiness readiness) =>
        app.Use(async (context, next) =>
        {
            if (!readiness.IsWaiting || DatabaseReadinessGate.Allows(context.Request.Path.Value))
            {
                await next();
                return;
            }

            context.Response.StatusCode = StatusCodes.Status503ServiceUnavailable;
            context.Response.Headers.RetryAfter = DatabaseReadinessGate.RetryAfterSeconds.ToString();
            context.Response.ContentType = "text/html; charset=utf-8";
            await context.Response.WriteAsync(
                "<!DOCTYPE html><html lang=\"en\"><head><meta charset=\"utf-8\"><meta name=\"viewport\" content=\"width=device-width, initial-scale=1\">" +
                "<title>Waiting for the database · DBTracker</title></head><body style=\"font-family:system-ui,sans-serif;max-width:40rem;margin:3rem auto;padding:0 1rem;line-height:1.5\">" +
                "<h1>DBTracker is waiting for its database</h1>" +
                "<p>DBTracker is running but can't reach SQL Server yet. This is normal for a minute or two after the server restarts. " +
                $"It tries again every {DatabaseReadinessGate.RetryAfterSeconds} seconds and opens by itself once the database answers.</p>" +
                "<p>If this page stays for more than a few minutes, check that the SQL Server service is running and that the app pool's account can sign in to it. " +
                "The Windows Application event log has the exact error.</p>" +
                "<p><a href=\"\">Try again now</a></p></body></html>");
        });
}
