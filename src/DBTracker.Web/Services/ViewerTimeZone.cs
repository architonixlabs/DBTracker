using DBTracker.Core.Services;
using Microsoft.JSInterop;

namespace DBTracker.Web.Services;

/// <summary>The viewer's browser time zone for this circuit (G11). Until the browser reports it (prerender, or JS unavailable) times are
/// shown in UTC, never in the server's own zone.</summary>
public class ViewerTimeZone
{
    private Task? _loading;

    public TimeZoneInfo Zone { get; private set; } = TimeZoneInfo.Utc;

    /// <summary>True once the browser has reported its zone.</summary>
    public bool Known { get; private set; }

    /// <summary>Raised once, when the browser's zone arrives, so components rendered in UTC can re-render.</summary>
    public event Action? Changed;

    public string ZoneId => EventTimeZones.IdOf(Zone);

    /// <summary>Call from OnAfterRenderAsync; only the first call talks to the browser.</summary>
    public Task EnsureLoadedAsync(IJSRuntime js) => _loading ??= LoadAsync(js);

    private async Task LoadAsync(IJSRuntime js)
    {
        try
        {
            var id = await js.InvokeAsync<string>("dbtracker.timeZone");
            if (EventTimeZones.Find(id) is { } zone)
            {
                Zone = zone;
            }
            Known = true;
            Changed?.Invoke();
        }
        catch (Exception)
        {
            // Prerender or a disconnected circuit: keep UTC and try again on the next render.
            _loading = null;
        }
    }

    public DateTime ToViewer(DateTime utc) => EventTimeZones.ToZone(utc, Zone);

    /// <summary>"UTC" or "Europe/London, UTC+01:00" for the given instant.</summary>
    public string Label(DateTime atUtc) => EventTimeZones.Label(Zone, atUtc);

    /// <summary>A viewer-local wall-clock time as UTC, with an error for a time skipped by daylight saving.</summary>
    public EventTimeResult FromViewer(DateTime wallClock) => EventTimeZones.FromZone(wallClock, Zone);

    /// <summary>Plain text for places a component can't go (option labels, CSV, toasts): "Nov 11, 2026 9:05 AM (UTC-05:00)".</summary>
    public string Format(DateTime utc, string format = "MMM d, yyyy h:mm tt")
    {
        var local = ToViewer(utc);
        return $"{local.ToString(format)} ({EventTimeZones.OffsetLabel(Zone.GetUtcOffset(DateTime.SpecifyKind(utc, DateTimeKind.Utc)))})";
    }
}
