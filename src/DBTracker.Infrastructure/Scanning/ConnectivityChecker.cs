using System.Diagnostics;
using System.Net.Sockets;

namespace DBTracker.Infrastructure.Scanning;

public record ConnectivityResult(bool Success, string? Error, TimeSpan Elapsed);

/// <summary>
/// A lightweight TCP reachability check for the "Test connection" button on the instance detail
/// page — proves the host:port accepts a connection, not that the database itself is healthy
/// (that's what backup records are for). Deliberately just a socket connect, no per-engine driver,
/// so it works the same way for every engine without adding five database client dependencies.
/// </summary>
public class ConnectivityChecker
{
    public async Task<ConnectivityResult> CheckAsync(string host, int port, TimeSpan timeout, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(host))
        {
            return new ConnectivityResult(false, "No host/IP configured.", TimeSpan.Zero);
        }

        if (port <= 0)
        {
            return new ConnectivityResult(false, "No known default port for this engine — configure one manually.", TimeSpan.Zero);
        }

        var stopwatch = Stopwatch.StartNew();
        using var client = new TcpClient();
        using var timeoutCts = CancellationTokenSource.CreateLinkedTokenSource(ct);
        timeoutCts.CancelAfter(timeout);

        try
        {
            await client.ConnectAsync(host, port, timeoutCts.Token);
            stopwatch.Stop();
            return new ConnectivityResult(true, null, stopwatch.Elapsed);
        }
        catch (OperationCanceledException) when (!ct.IsCancellationRequested)
        {
            stopwatch.Stop();
            return new ConnectivityResult(false, $"Timed out after {timeout.TotalSeconds:N0}s.", stopwatch.Elapsed);
        }
        catch (SocketException ex)
        {
            stopwatch.Stop();
            return new ConnectivityResult(false, ex.Message, stopwatch.Elapsed);
        }
    }
}
