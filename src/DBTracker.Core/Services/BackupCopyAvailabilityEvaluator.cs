using DBTracker.Core.Entities;
using DBTracker.Core.Enums;

namespace DBTracker.Core.Services;

public record CopyAvailabilityResult(bool IsCurrentlyVerifiedAvailable, bool HasStaleVerification, string Explanation);

/// <summary>
/// D17 — pure classification of one backup-copy location's current availability, kept explicitly
/// separate from U01's EvidenceLevel/"we once discovered a file" framing: a copy's FirstDiscoveredAtUtc
/// is historical provenance only, never treated here as proof the copy is still there today. Only a
/// recent Accessible verification counts as currently available.
/// </summary>
public static class BackupCopyAvailabilityEvaluator
{
    public static CopyAvailabilityResult Evaluate(BackupCopyLocation copy, DateTime nowUtc, int staleAfterDays)
    {
        if (copy.VerificationStatus == CopyVerificationStatus.NeverVerified)
        {
            var explanation = copy.FirstDiscoveredAtUtc is { } discovered
                ? $"Discovered on {discovered:u}, but never actually verified since — historical discovery is not current availability."
                : "Never verified.";
            return new CopyAvailabilityResult(false, false, explanation);
        }

        var isStale = copy.LastVerifiedAtUtc is null || copy.LastVerifiedAtUtc.Value.AddDays(staleAfterDays) <= nowUtc;

        return copy.VerificationStatus switch
        {
            CopyVerificationStatus.Accessible when isStale =>
                new CopyAvailabilityResult(false, true, $"Last verified accessible on {copy.LastVerifiedAtUtc:u}, which is now stale (older than {staleAfterDays} day(s)) — re-verify before relying on it."),
            CopyVerificationStatus.Accessible =>
                new CopyAvailabilityResult(true, false, $"Currently verified accessible (as of {copy.LastVerifiedAtUtc:u})."),
            CopyVerificationStatus.CannotVerify =>
                new CopyAvailabilityResult(false, isStale, "Last check could not confirm the copy — not the same claim as confirmed missing."),
            CopyVerificationStatus.Missing =>
                new CopyAvailabilityResult(false, isStale, "Last check confirmed the copy is missing."),
            _ => new CopyAvailabilityResult(false, isStale, "Unverified.")
        };
    }
}
