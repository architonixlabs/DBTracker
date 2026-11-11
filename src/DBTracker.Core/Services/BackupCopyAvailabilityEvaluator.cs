using DBTracker.Core.Entities;
using DBTracker.Core.Enums;

namespace DBTracker.Core.Services;

public record CopyAvailabilityResult(bool IsCurrentlyVerifiedAvailable, bool HasStaleVerification, string Explanation);

/// <summary>Current availability of one backup copy. Only a recent Accessible verification counts; FirstDiscoveredAtUtc is history, not proof the copy is still there.</summary>
public static class BackupCopyAvailabilityEvaluator
{
    public static CopyAvailabilityResult Evaluate(BackupCopyLocation copy, DateTime nowUtc, int staleAfterDays)
    {
        if (copy.VerificationStatus == CopyVerificationStatus.NeverVerified)
        {
            var explanation = copy.FirstDiscoveredAtUtc is { } discovered
                ? $"Discovered on {discovered:u}, but never verified since. A historical discovery is not proof of current availability."
                : "Never verified.";
            return new CopyAvailabilityResult(false, false, explanation);
        }

        var isStale = copy.LastVerifiedAtUtc is null || copy.LastVerifiedAtUtc.Value.AddDays(staleAfterDays) <= nowUtc;

        return copy.VerificationStatus switch
        {
            CopyVerificationStatus.Accessible when isStale =>
                new CopyAvailabilityResult(false, true, $"Last verified accessible on {copy.LastVerifiedAtUtc:u}, which is now stale (older than {staleAfterDays} day(s)). Re-verify before relying on it."),
            CopyVerificationStatus.Accessible =>
                new CopyAvailabilityResult(true, false, $"Currently verified accessible (as of {copy.LastVerifiedAtUtc:u})."),
            CopyVerificationStatus.CannotVerify =>
                new CopyAvailabilityResult(false, isStale, "Last check could not confirm the copy. That is not the same as confirming it missing."),
            CopyVerificationStatus.Missing =>
                new CopyAvailabilityResult(false, isStale, "Last check confirmed the copy is missing."),
            _ => new CopyAvailabilityResult(false, isStale, "Unverified.")
        };
    }
}
