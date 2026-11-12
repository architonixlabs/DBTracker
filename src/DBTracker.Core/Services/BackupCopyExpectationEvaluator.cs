using DBTracker.Core.Entities;
using DBTracker.Core.Enums;

namespace DBTracker.Core.Services;

public enum CopyExpectationStatus
{
    /// <summary>No expectation recorded, or the evidence can't settle it. Never shown as met.</summary>
    Unknown,
    NotRequired,
    Met,
    Gap
}

public record CopyExpectationResult(string Expectation, CopyExpectationStatus Status, string Explanation);

/// <summary>Checks recorded copy expectations (offsite, immutable, encrypted) and legal holds against the copies that are currently verified
/// available. A copy that was only discovered, or whose check is stale, never satisfies an expectation; an unrecorded expectation is Unknown.</summary>
public static class BackupCopyExpectationEvaluator
{
    public static IReadOnlyList<CopyExpectationResult> Evaluate(RecoveryObjective? objective, IReadOnlyCollection<BackupCopyLocation> copies, DateTime nowUtc, int staleAfterDays)
    {
        var available = copies.Where(c => BackupCopyAvailabilityEvaluator.Evaluate(c, nowUtc, staleAfterDays).IsCurrentlyVerifiedAvailable).ToList();

        return
        [
            Presence("Offsite copy", objective?.RequiresOffsiteCopy, copies.Where(c => c.IsOffsite).ToList(), available.Where(c => c.IsOffsite).ToList(),
                metNote: ""),
            Presence("Immutable copy", objective?.RequiresImmutableCopy, copies.Where(c => c.IsImmutableFlagged).ToList(), available.Where(c => c.IsImmutableFlagged).ToList(),
                metNote: " Immutability is as flagged by whoever recorded the copy; DBTracker can't confirm it with the storage provider."),
            Encryption(objective?.RequiresEncryptedCopies, copies, available),
            LegalHolds(copies, nowUtc, staleAfterDays)
        ];
    }

    private static CopyExpectationResult Presence(string name, bool? required, IReadOnlyList<BackupCopyLocation> recorded, IReadOnlyList<BackupCopyLocation> availableMatching, string metNote)
    {
        if (required is null)
        {
            return new(name, CopyExpectationStatus.Unknown, $"No expectation recorded for an {name.ToLowerInvariant()}. Set one in the recovery objectives.");
        }
        if (required == false)
        {
            return new(name, CopyExpectationStatus.NotRequired, $"Recovery objectives say an {name.ToLowerInvariant()} is not required.");
        }
        if (availableMatching.Count > 0)
        {
            return new(name, CopyExpectationStatus.Met,
                $"Required, and {string.Join(", ", availableMatching.Select(c => c.Label))} is currently verified accessible.{metNote}");
        }
        return recorded.Count > 0
            ? new(name, CopyExpectationStatus.Gap,
                $"Required. {string.Join(", ", recorded.Select(c => c.Label))} is recorded, but not currently verified accessible.")
            : new(name, CopyExpectationStatus.Gap, $"Required, but no {name.ToLowerInvariant()} is recorded.");
    }

    private static CopyExpectationResult Encryption(bool? requiredByObjective, IReadOnlyCollection<BackupCopyLocation> copies, IReadOnlyList<BackupCopyLocation> available)
    {
        const string name = "Encrypted copies";
        var expectedOnCopies = copies.Where(c => c.EncryptionExpected).ToList();
        var required = requiredByObjective == true || expectedOnCopies.Count > 0;

        if (!required)
        {
            return requiredByObjective == false
                ? new(name, CopyExpectationStatus.NotRequired, "Recovery objectives say copies don't need to be encrypted.")
                : new(name, CopyExpectationStatus.Unknown, "No encryption expectation recorded for this instance's copies.");
        }

        // Copies in scope: every copy when the objective requires encryption, otherwise only those individually expected to be encrypted.
        var inScope = requiredByObjective == true ? copies.ToList() : expectedOnCopies;
        var notEncrypted = inScope.Where(c => c.EncryptionConfirmed == false).ToList();
        if (notEncrypted.Count > 0)
        {
            return new(name, CopyExpectationStatus.Gap, $"Encryption is expected, but the last check found {string.Join(", ", notEncrypted.Select(c => c.Label))} not encrypted.");
        }

        var availableInScope = inScope.Where(available.Contains).ToList();
        if (availableInScope.Count == 0)
        {
            return new(name, CopyExpectationStatus.Gap, "Encryption is expected, but no copy in scope is currently verified accessible.");
        }

        var unchecked_ = inScope.Where(c => c.EncryptionConfirmed is null).ToList();
        if (unchecked_.Count > 0)
        {
            return new(name, CopyExpectationStatus.Unknown, $"Encryption is expected, but it hasn't been checked on {string.Join(", ", unchecked_.Select(c => c.Label))}.");
        }

        return new(name, CopyExpectationStatus.Met, "Encryption is expected and was confirmed on every copy in scope.");
    }

    private static CopyExpectationResult LegalHolds(IReadOnlyCollection<BackupCopyLocation> copies, DateTime nowUtc, int staleAfterDays)
    {
        const string name = "Legal holds";
        var held = copies.Where(c => !string.IsNullOrWhiteSpace(c.LegalHoldReference)).ToList();
        if (held.Count == 0)
        {
            return new(name, CopyExpectationStatus.NotRequired, "No copy has a legal-hold reference recorded.");
        }

        var missing = held.Where(c => c.VerificationStatus == CopyVerificationStatus.Missing).ToList();
        if (missing.Count > 0)
        {
            return new(name, CopyExpectationStatus.Gap,
                $"A copy under legal hold was confirmed missing: {string.Join(", ", missing.Select(c => $"{c.Label} (hold {c.LegalHoldReference})"))}.");
        }

        var unverified = held.Where(c => !BackupCopyAvailabilityEvaluator.Evaluate(c, nowUtc, staleAfterDays).IsCurrentlyVerifiedAvailable).ToList();
        if (unverified.Count > 0)
        {
            return new(name, CopyExpectationStatus.Unknown,
                $"Copies under legal hold aren't currently verified: {string.Join(", ", unverified.Select(c => $"{c.Label} (hold {c.LegalHoldReference})"))}.");
        }

        return new(name, CopyExpectationStatus.Met,
            $"Every copy under legal hold is currently verified accessible: {string.Join(", ", held.Select(c => $"{c.Label} (hold {c.LegalHoldReference})"))}.");
    }
}
