namespace DBTracker.Core.Services.Install;

/// <summary>Sliding-window cap on failed attempts per client. Thread-safe; the clock is injectable for tests.</summary>
public sealed class AttemptLimiter(int maxFailures, TimeSpan window, Func<DateTime>? utcNow = null)
{
    private readonly Func<DateTime> _utcNow = utcNow ?? (() => DateTime.UtcNow);
    private readonly Dictionary<string, Queue<DateTime>> _failures = new(StringComparer.Ordinal);
    private readonly Lock _gate = new();

    public bool IsLockedOut(string client, out TimeSpan retryAfter)
    {
        lock (_gate)
        {
            var queue = Prune(client);
            if (queue is null || queue.Count < maxFailures)
            {
                retryAfter = TimeSpan.Zero;
                return false;
            }

            retryAfter = queue.Peek() + window - _utcNow();
            if (retryAfter < TimeSpan.Zero)
            {
                retryAfter = TimeSpan.Zero;
            }
            return true;
        }
    }

    public void RecordFailure(string client)
    {
        lock (_gate)
        {
            var queue = Prune(client) ?? (_failures[client] = new Queue<DateTime>());
            queue.Enqueue(_utcNow());
        }
    }

    public void Reset(string client)
    {
        lock (_gate)
        {
            _failures.Remove(client);
        }
    }

    private Queue<DateTime>? Prune(string client)
    {
        if (!_failures.TryGetValue(client, out var queue))
        {
            return null;
        }

        var cutoff = _utcNow() - window;
        while (queue.Count > 0 && queue.Peek() <= cutoff)
        {
            queue.Dequeue();
        }
        return queue;
    }
}
