using DBTracker.Core.Entities;
using DBTracker.Core.Enums;

namespace DBTracker.Core.Services;

/// <summary>Result of evaluating one release-catalogue entry's support runway as of a point in time (D03).</summary>
public record SupportRunwayResult(
    SupportPhase Phase,
    string? NextMilestoneLabel,
    DateTime? NextMilestoneUtc,
    int? DaysToNextMilestone,
    bool IsUnknown,
    string UnknownReason)
{
    public static SupportRunwayResult Unknown(string reason) =>
        new(SupportPhase.Unknown, null, null, null, true, reason);
}

/// <summary>
/// Pure support-phase/runway math (D03) — no fixed universal EOL formula; it only walks the
/// milestones a reviewed ReleaseCatalogEntry actually carries. TBD/conflicting dates and a missing
/// catalogue match both produce an explicit Unknown rather than guessing. Once a milestone has
/// passed, DaysToNextMilestone goes negative (overdue) rather than disappearing — overdue work must
/// stay visible, not silently clear itself.
/// </summary>
public static class SupportRunwayCalculator
{
    public static SupportRunwayResult Compute(ReleaseCatalogEntry? entry, DateTime nowUtc)
    {
        if (entry is null)
        {
            return SupportRunwayResult.Unknown("No matching release-catalogue entry for this instance's resolved identity.");
        }

        if (entry.DatesAreTbd)
        {
            return SupportRunwayResult.Unknown("Catalogue entry has TBD support dates.");
        }

        if (entry.DatesConflict)
        {
            return SupportRunwayResult.Unknown("Catalogue entry has conflicting source dates pending review.");
        }

        var milestones = new List<(SupportPhase Phase, DateTime Date, DatePrecision Precision)>();
        if (entry.MainstreamSupportEndUtc is { } mainstream && entry.MainstreamSupportEndPrecision != DatePrecision.Unknown)
        {
            milestones.Add((SupportPhase.Mainstream, mainstream, entry.MainstreamSupportEndPrecision));
        }
        if (entry.ExtendedSupportEndUtc is { } extended && entry.ExtendedSupportEndPrecision != DatePrecision.Unknown)
        {
            milestones.Add((SupportPhase.Extended, extended, entry.ExtendedSupportEndPrecision));
        }
        if (entry.EsuSupportEndUtc is { } esu && entry.EsuSupportEndPrecision != DatePrecision.Unknown)
        {
            milestones.Add((SupportPhase.Esu, esu, entry.EsuSupportEndPrecision));
        }

        if (milestones.Count == 0)
        {
            return SupportRunwayResult.Unknown("Catalogue entry has no usable support-milestone dates.");
        }

        milestones.Sort((a, b) => a.Date.CompareTo(b.Date));

        var upcoming = milestones.FirstOrDefault(m => m.Date >= nowUtc);
        if (upcoming != default)
        {
            var days = (int)Math.Ceiling((upcoming.Date - nowUtc).TotalDays);
            return new SupportRunwayResult(upcoming.Phase, $"{Label(upcoming.Phase)} support ends", upcoming.Date, days, false, "");
        }

        // Every known milestone has already passed.
        var last = milestones[^1];
        var overdueDays = (int)Math.Ceiling((last.Date - nowUtc).TotalDays); // negative
        return new SupportRunwayResult(SupportPhase.EndOfLife, $"{Label(last.Phase)} support ended", last.Date, overdueDays, false, "");
    }

    private static string Label(SupportPhase phase) => phase switch
    {
        SupportPhase.Mainstream => "Mainstream",
        SupportPhase.Extended => "Extended",
        SupportPhase.Esu => "ESU",
        _ => phase.ToString()
    };
}
