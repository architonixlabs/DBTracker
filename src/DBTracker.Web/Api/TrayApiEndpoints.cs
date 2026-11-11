using System.Reflection;
using System.Security.Claims;
using DBTracker.Core.Services;
using DBTracker.Infrastructure;
using DBTracker.Infrastructure.Install;
using DBTracker.Infrastructure.Queries;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace DBTracker.Web.Api;

public static class TrayApiEndpoints
{
    private static readonly TimeSpan DatabaseProbeTimeout = TimeSpan.FromSeconds(3);
    private static readonly TimeSpan DatabaseProbeCacheFor = TimeSpan.FromSeconds(10);
    private static readonly SemaphoreSlim ProbeLock = new(1, 1);
    private static (DateTime CheckedAtUtc, bool Reachable)? _lastProbe;

    private static readonly string AppVersion = FormatVersion(Assembly.GetEntryAssembly()?.GetName().Version);

    public static IEndpointRouteBuilder MapTrayApi(this IEndpointRouteBuilder endpoints)
    {
        endpoints.MapGet("/healthz", async (
            [FromServices] InstallState installState,
            [FromServices] IDbContextFactory<DBTrackerDbContext> dbFactory,
            HttpContext context) =>
        {
            if (!installState.IsInstalled)
            {
                return Results.Ok(new HealthResponse("setup-required", false, "not-configured", AppVersion));
            }

            var reachable = await IsDatabaseReachableAsync(dbFactory, context.RequestAborted);
            return Results.Ok(new HealthResponse(reachable ? "ok" : "degraded", true, reachable ? "ok" : "unreachable", AppVersion));
        }).AllowAnonymous().DisableAntiforgery();

        endpoints.MapGet("/api/tray/summary", async (ClaimsPrincipal user, [FromServices] InstanceOverviewService overview) =>
        {
            var rows = await overview.GetAllSummariesAsync();
            return Results.Ok(TraySummaryBuilder.Build(rows, user.Identity?.Name ?? "", DateTime.UtcNow));
        }).RequireAuthorization(AccessTokenDefaults.Policy);

        return endpoints;
    }

    // Cached briefly so many tray apps polling at once cost one connection attempt, not one each.
    private static async Task<bool> IsDatabaseReachableAsync(IDbContextFactory<DBTrackerDbContext> dbFactory, CancellationToken requestAborted)
    {
        if (_lastProbe is { } cached && DateTime.UtcNow - cached.CheckedAtUtc < DatabaseProbeCacheFor)
        {
            return cached.Reachable;
        }

        await ProbeLock.WaitAsync(requestAborted);
        try
        {
            if (_lastProbe is { } fresh && DateTime.UtcNow - fresh.CheckedAtUtc < DatabaseProbeCacheFor)
            {
                return fresh.Reachable;
            }

            bool reachable;
            try
            {
                using var timeout = CancellationTokenSource.CreateLinkedTokenSource(requestAborted);
                timeout.CancelAfter(DatabaseProbeTimeout);
                await using var db = await dbFactory.CreateDbContextAsync(timeout.Token);
                reachable = await db.Database.CanConnectAsync(timeout.Token);
            }
            catch (Exception) when (!requestAborted.IsCancellationRequested)
            {
                reachable = false;
            }

            _lastProbe = (DateTime.UtcNow, reachable);
            return reachable;
        }
        finally
        {
            ProbeLock.Release();
        }
    }

    private static string FormatVersion(Version? version) =>
        version is null ? "0.0.0" : $"{version.Major}.{version.Minor}.{Math.Max(version.Build, 0)}";

    private sealed record HealthResponse(string Status, bool Installed, string Database, string Version);
}
