using DBTracker.Core.Entities;
using DBTracker.Core.Enums;

namespace DBTracker.Core.Services;

/// <summary>Support runway for one catalogue entry as of a point in time.</summary>
/// <param name="NeedsReview">Facts exist but disagree or have lapsed. Kept apart from plain Unknown (no usable facts).</param>
/// <param name="CoverageNote">What contract evidence was or wasn't counted for extended/ESU coverage. Empty when none applies.</param>
public record SupportRunwayResult(
    SupportPhase Phase,
    string? NextMilestoneLabel,
    DateTime? NextMilestoneUtc,
    int? DaysToNextMilestone,
    bool IsUnknown,
    string UnknownReason,
    bool NeedsReview = false,
    string CoverageNote = "")
{
    public static SupportRunwayResult Unknown(string reason) =>
        new(SupportPhase.Unknown, null, null, null, true, reason);

    public static SupportRunwayResult NeedsReviewResult(string reason) =>
        new(SupportPhase.NeedsReview, null, null, null, true, reason, NeedsReview: true);
}

/// <summary>Support-phase and runway math from the milestones an entry actually carries; no universal EOL formula. TBD dates and missing matches are Unknown, and passed milestones go negative instead of disappearing.</summary>
public static class SupportRunwayCalculator
{
    /// <summary>Vendor milestones only: every catalogued date counts, including ESU. Use Evaluate for an instance, which
    /// also checks the identity, override expiry and contract evidence.</summary>
    public static SupportRunwayResult Compute(ReleaseCatalogEntry? entry, DateTime nowUtc)
    {
        var precheck = Precheck(entry, nowUtc, checkOverrideExpiry: false);
        if (precheck is not null)
        {
            return precheck;
        }

        return FromMilestones(VendorMilestones(entry!, includeEsu: true), nowUtc, "");
    }

    /// <summary>The runway for one instance. Unknown means there are no usable facts (no identity, an unresolved
    /// version, no catalogue match, TBD or missing dates). Needs review means facts exist but can't be relied on (a
    /// version flagged for review, conflicting dates, an expired manual override). ESU dates count only while an active
    /// Extended coverage contract is on file, and never past that contract's end, so paid coverage is never implied.</summary>
    public static SupportRunwayResult Evaluate(VersionIdentity? identity, ReleaseCatalogEntry? entry,
        IReadOnlyCollection<SupportContract> contracts, DateTime nowUtc)
    {
        if (identity is null)
        {
            return SupportRunwayResult.Unknown("No version identity is recorded for this instance. Record its version on the instance lifecycle page.");
        }

        if (identity.ResolutionStatus == VersionResolutionStatus.NeedsReview)
        {
            return SupportRunwayResult.NeedsReviewResult(
                $"The recorded version needs review before a support runway can be worked out. {identity.UnresolvedReason}".Trim());
        }

        if (identity.ResolutionStatus != VersionResolutionStatus.Resolved)
        {
            return SupportRunwayResult.Unknown("The recorded version isn't resolved to a known release.");
        }

        var precheck = Precheck(entry, nowUtc, checkOverrideExpiry: true);
        if (precheck is not null)
        {
            return precheck;
        }

        var milestones = VendorMilestones(entry!, includeEsu: false);
        var extendedContract = ActiveExtendedContract(contracts, nowUtc);
        string coverageNote;

        if (entry!.EsuSupportEndUtc is { } esuEnd && entry.EsuSupportEndPrecision != DatePrecision.Unknown)
        {
            if (extendedContract is null)
            {
                coverageNote = $"ESU dates are catalogued (to {esuEnd:yyyy-MM-dd}), but no active Extended coverage contract is on file, so ESU coverage is not counted.";
            }
            else
            {
                var coveredUntil = extendedContract.CoverageEndUtc is { } contractEnd && contractEnd < esuEnd ? contractEnd : esuEnd;
                var label = coveredUntil < esuEnd ? "ESU coverage under contract" : "ESU";
                milestones.Add((SupportPhase.Esu, coveredUntil, entry.EsuSupportEndPrecision, label));
                coverageNote = extendedContract.CoverageEndUtc is null
                    ? $"ESU counted on the Extended contract from {extendedContract.Vendor}, which has no recorded end date. Check the contract end."
                    : $"ESU counted on the Extended contract from {extendedContract.Vendor}, which ends {extendedContract.CoverageEndUtc:yyyy-MM-dd}.";
            }
        }
        else
        {
            coverageNote = extendedContract is null
                ? ""
                : $"An Extended coverage contract from {extendedContract.Vendor} is on file{(extendedContract.CoverageEndUtc is { } end ? $" until {end:yyyy-MM-dd}" : "")}. It doesn't change the vendor dates.";
        }

        if (milestones.Count == 0)
        {
            return SupportRunwayResult.Unknown("Catalogue entry has no usable support-milestone dates.") with { CoverageNote = coverageNote };
        }

        return FromMilestones(milestones, nowUtc, coverageNote);
    }

    /// <summary>The active contract of type Extended that runs longest, or null. A contract with no end date counts as active.</summary>
    public static SupportContract? ActiveExtendedContract(IReadOnlyCollection<SupportContract> contracts, DateTime nowUtc) =>
        contracts
            .Where(c => c.CoverageType == SupportCoverageType.Extended &&
                        (c.CoverageStartUtc is null || c.CoverageStartUtc <= nowUtc) &&
                        (c.CoverageEndUtc is null || c.CoverageEndUtc > nowUtc))
            .OrderByDescending(c => c.CoverageEndUtc ?? DateTime.MaxValue)
            .FirstOrDefault();

    private static SupportRunwayResult? Precheck(ReleaseCatalogEntry? entry, DateTime nowUtc, bool checkOverrideExpiry)
    {
        if (entry is null)
        {
            return SupportRunwayResult.Unknown("No matching release-catalogue entry for this instance's resolved identity.");
        }

        if (checkOverrideExpiry && ReleaseCatalogMatcher.IsOverrideExpired(entry, nowUtc))
        {
            return SupportRunwayResult.NeedsReviewResult(
                $"The manual override on \"{entry.ReleaseLabel}\" expired on {entry.OverrideExpiresUtc:yyyy-MM-dd}. Its dates aren't used until someone reviews the entry.");
        }

        if (entry.DatesConflict)
        {
            return checkOverrideExpiry
                ? SupportRunwayResult.NeedsReviewResult("Catalogue entry has conflicting source dates pending review.")
                : SupportRunwayResult.Unknown("Catalogue entry has conflicting source dates pending review.");
        }

        if (entry.DatesAreTbd)
        {
            return SupportRunwayResult.Unknown("Catalogue entry has TBD support dates.");
        }

        return null;
    }

    private static List<(SupportPhase Phase, DateTime Date, DatePrecision Precision, string Label)> VendorMilestones(ReleaseCatalogEntry entry, bool includeEsu)
    {
        var milestones = new List<(SupportPhase Phase, DateTime Date, DatePrecision Precision, string Label)>();
        if (entry.MainstreamSupportEndUtc is { } mainstream && entry.MainstreamSupportEndPrecision != DatePrecision.Unknown)
        {
            milestones.Add((SupportPhase.Mainstream, mainstream, entry.MainstreamSupportEndPrecision, "Mainstream"));
        }
        if (entry.ExtendedSupportEndUtc is { } extended && entry.ExtendedSupportEndPrecision != DatePrecision.Unknown)
        {
            milestones.Add((SupportPhase.Extended, extended, entry.ExtendedSupportEndPrecision, "Extended"));
        }
        if (includeEsu && entry.EsuSupportEndUtc is { } esu && entry.EsuSupportEndPrecision != DatePrecision.Unknown)
        {
            milestones.Add((SupportPhase.Esu, esu, entry.EsuSupportEndPrecision, "ESU"));
        }
        return milestones;
    }

    private static SupportRunwayResult FromMilestones(List<(SupportPhase Phase, DateTime Date, DatePrecision Precision, string Label)> milestones, DateTime nowUtc, string coverageNote)
    {
        if (milestones.Count == 0)
        {
            return SupportRunwayResult.Unknown("Catalogue entry has no usable support-milestone dates.");
        }

        milestones.Sort((a, b) => a.Date.CompareTo(b.Date));

        var upcoming = milestones.FirstOrDefault(m => m.Date >= nowUtc);
        if (upcoming != default)
        {
            var days = (int)Math.Ceiling((upcoming.Date - nowUtc).TotalDays);
            return new SupportRunwayResult(upcoming.Phase, $"{upcoming.Label} support ends", upcoming.Date, days, false, "", CoverageNote: coverageNote);
        }

        // Every known milestone has already passed.
        var last = milestones[^1];
        var overdueDays = (int)Math.Ceiling((last.Date - nowUtc).TotalDays); // negative
        return new SupportRunwayResult(SupportPhase.EndOfLife, $"{last.Label} support ended", last.Date, overdueDays, false, "", CoverageNote: coverageNote);
    }
}
