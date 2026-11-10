using System.Collections.Concurrent;

namespace DBTracker.Infrastructure.Diagnostics;

public record WorkerHeartbeat(string Name, bool Enabled, DateTime? LastRunUtc, DateTime? NextRunUtc, string? LastError, TimeSpan? Interval);

/// <summary>
/// In-memory record of each background worker's last/next run, for the Admin setup/diagnostics
/// page (U15) — "is the scan/alert/report sweep actually running" shouldn't require reading the
/// server's log file. Process-lifetime only (reset on restart); that's fine for "is it running right
/// now", which is what the diagnostics page is answering.
/// </summary>
public class WorkerHeartbeatStore
{
    private readonly ConcurrentDictionary<string, WorkerHeartbeat> _heartbeats = new();

    public void Disabled(string name) => _heartbeats[name] = new WorkerHeartbeat(name, false, null, null, null, null);

    public void Starting(string name, TimeSpan interval) =>
        _heartbeats[name] = new WorkerHeartbeat(name, true, null, DateTime.UtcNow, null, interval);

    public void RanSuccessfully(string name, TimeSpan interval)
    {
        var now = DateTime.UtcNow;
        _heartbeats[name] = new WorkerHeartbeat(name, true, now, now.Add(interval), null, interval);
    }

    public void RanWithError(string name, TimeSpan interval, string error)
    {
        var now = DateTime.UtcNow;
        _heartbeats[name] = new WorkerHeartbeat(name, true, now, now.Add(interval), error, interval);
    }

    public WorkerHeartbeat? Get(string name) => _heartbeats.GetValueOrDefault(name);

    public IReadOnlyList<WorkerHeartbeat> All() => _heartbeats.Values.OrderBy(h => h.Name).ToList();
}
