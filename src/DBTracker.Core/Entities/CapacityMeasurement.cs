using DBTracker.Core.Enums;

namespace DBTracker.Core.Entities;

/// <summary>
/// D19 — one timestamped capacity/performance measurement (growth, free space, workload, or a
/// performance baseline), imported or referenced from existing monitoring rather than collected by a
/// new continuous profiler built into this app. Unit/MeasurementPeriodDescription/Source are explicit
/// so a reader never has to guess what a bare number means; freshness is always derived live from
/// MeasuredAtUtc vs. "now", never stored as a separate stale/fresh flag that could drift out of sync.
/// </summary>
public class CapacityMeasurement
{
    public int Id { get; set; }

    public int DatabaseInstanceId { get; set; }
    public DatabaseInstance? DatabaseInstance { get; set; }

    public CapacityMeasurementType MeasurementType { get; set; }

    public DateTime MeasuredAtUtc { get; set; } = DateTime.UtcNow;
    public double Value { get; set; }
    public string Unit { get; set; } = string.Empty;

    /// <summary>E.g. "30-day average", "point-in-time snapshot", "peak during month-end close".</summary>
    public string MeasurementPeriodDescription { get; set; } = string.Empty;

    /// <summary>Free text: which monitoring system/export/manual observation this came from.</summary>
    public string Source { get; set; } = string.Empty;

    public string Notes { get; set; } = string.Empty;

    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;
    public string RecordedBy { get; set; } = string.Empty;
}
