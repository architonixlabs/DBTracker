using System.Diagnostics;
using System.Net.Sockets;

namespace DBTracker.Infrastructure.Scanning;

public record ConnectivityResult(bool Success, string? Error, TimeSpan Elapsed, DateTime CheckedAtUtc, TimeSpan Timeout);

/// <summary>A plain TCP connect for Test connection. Proves host:port accepts connections, not that the database is healthy; no per-engine driver needed.</summary>
public class ConnectivityChecker
{
    public async Task<ConnectivityResult> CheckAsync(string host, int port, TimeSpan timeout, CancellationToken ct = default)
    {
        var checkedAtUtc = DateTime.UtcNow;

        if (string.IsNullOrWhiteSpace(host))
        {
            return new ConnectivityResult(false, "No host/IP configured.", TimeSpan.Zero, checkedAtUtc, timeout);
        }

        if (port <= 0)
        {
            return new ConnectivityResult(false, "No known default port for this engine — configure one manually.", TimeSpan.Zero, checkedAtUtc, timeout);
        }

        var stopwatch = Stopwatch.StartNew();
        using var client = new TcpClient();
        using var timeoutCts = CancellationTokenSource.CreateLinkedTokenSource(ct);
        timeoutCts.CancelAfter(timeout);

        try
        {
            await client.ConnectAsync(host, port, timeoutCts.Token);
            stopwatch.Stop();
            return new ConnectivityResult(true, null, stopwatch.Elapsed, checkedAtUtc, timeout);
        }
        catch (OperationCanceledException) when (!ct.IsCancellationRequested)
        {
            stopwatch.Stop();
            return new ConnectivityResult(false, $"Timed out after {timeout.TotalSeconds:N0}s — host may be unreachable, firewalled, or not listening on this port.", stopwatch.Elapsed, checkedAtUtc, timeout);
        }
        catch (SocketException ex)
        {
            stopwatch.Stop();
            return new ConnectivityResult(false, ex.Message, stopwatch.Elapsed, checkedAtUtc, timeout);
        }
    }
}
