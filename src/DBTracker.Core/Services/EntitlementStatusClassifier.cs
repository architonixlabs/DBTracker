using DBTracker.Core.Entities;
using DBTracker.Core.Enums;

namespace DBTracker.Core.Services;

public record EntitlementResult(
    EntitlementStatus Status,
    SupportContract? MostFavorableContract,
    int? DaysRemaining,
    string Detail);

/// <summary>
/// D21 — pure classification of one instance's support-entitlement status from whatever
/// SupportContract rows are actually assigned to it. Deliberately reads nothing from D01-D03 (no
/// VersionIdentity, ReleaseCatalogEntry or SupportRunwayDecision) — a product's vendor-lifecycle
/// phase and its paid-entitlement status are two separate facts that must be able to disagree (e.g.
/// Mainstream-supported with no contract on file, or past Mainstream with Active paid Extended
/// coverage). No contracts assigned is its own distinct status, never silently treated as Expired.
/// </summary>
public static class EntitlementStatusClassifier
{
    public static EntitlementResult Classify(IReadOnlyCollection<SupportContract> contracts, DateTime nowUtc, int expiringSoonThresholdDays = 60)
    {
        if (contracts.Count == 0)
        {
            return new EntitlementResult(EntitlementStatus.NoContractOnFile, null, null,
                "No support contract has been linked to this instance.");
        }

        // Prefer whichever contract currently covers "now" and runs longest; an open-ended
        // (null end date) contract is treated as currently active with no known expiry.
        var currentlyCovering = contracts
            .Where(c => (c.CoverageStartUtc is null || c.CoverageStartUtc <= nowUtc) &&
                        (c.CoverageEndUtc is null || c.CoverageEndUtc > nowUtc))
            .OrderByDescending(c => c.CoverageEndUtc ?? DateTime.MaxValue)
            .FirstOrDefault();

        if (currentlyCovering is not null)
        {
            if (currentlyCovering.CoverageEndUtc is null)
            {
                return new EntitlementResult(EntitlementStatus.Active, currentlyCovering, null,
                    $"Active {currentlyCovering.CoverageType} coverage from {currentlyCovering.Vendor} with no recorded end date.");
            }

            var daysRemaining = (int)Math.Ceiling((currentlyCovering.CoverageEndUtc.Value - nowUtc).TotalDays);
            if (daysRemaining <= expiringSoonThresholdDays)
            {
                return new EntitlementResult(EntitlementStatus.ExpiringSoon, currentlyCovering, daysRemaining,
                    $"{currentlyCovering.CoverageType} coverage from {currentlyCovering.Vendor} expires in {daysRemaining} day(s) ({currentlyCovering.CoverageEndUtc:yyyy-MM-dd}).");
            }

            return new EntitlementResult(EntitlementStatus.Active, currentlyCovering, daysRemaining,
                $"Active {currentlyCovering.CoverageType} coverage from {currentlyCovering.Vendor} through {currentlyCovering.CoverageEndUtc:yyyy-MM-dd}.");
        }

        // Nothing currently covers "now" — report the most recently lapsed contract, if any.
        var mostRecentlyExpired = contracts
            .Where(c => c.CoverageEndUtc is not null && c.CoverageEndUtc <= nowUtc)
            .OrderByDescending(c => c.CoverageEndUtc)
            .FirstOrDefault();

        if (mostRecentlyExpired is not null)
        {
            var daysOverdue = (int)Math.Ceiling((nowUtc - mostRecentlyExpired.CoverageEndUtc!.Value).TotalDays);
            return new EntitlementResult(EntitlementStatus.Expired, mostRecentlyExpired, -daysOverdue,
                $"{mostRecentlyExpired.CoverageType} coverage from {mostRecentlyExpired.Vendor} expired {daysOverdue} day(s) ago ({mostRecentlyExpired.CoverageEndUtc:yyyy-MM-dd}).");
        }

        // Contracts exist (e.g. with a future CoverageStartUtc) but none is current or lapsed yet.
        var upcoming = contracts.OrderBy(c => c.CoverageStartUtc ?? DateTime.MaxValue).First();
        return new EntitlementResult(EntitlementStatus.Expired, upcoming, null,
            $"No currently effective coverage — the recorded {upcoming.CoverageType} agreement from {upcoming.Vendor} does not cover today.");
    }
}
