using DBTracker.Core.Entities;
using DBTracker.Core.Enums;
using DBTracker.Core.Services;
using Xunit;

namespace DBTracker.UnitTests;

public class BackupCopyExpectationEvaluatorTests
{
    private static readonly DateTime Now = new(2026, 11, 11, 0, 0, 0, DateTimeKind.Utc);
    private const int StaleDays = 30;

    private static BackupCopyLocation Copy(string label, CopyVerificationStatus status = CopyVerificationStatus.Accessible, int verifiedDaysAgo = 1,
        bool offsite = false, bool immutable = false, bool encryptionExpected = false, bool? encrypted = null, string hold = "") => new()
    {
        Label = label,
        VerificationStatus = status,
        LastVerifiedAtUtc = status == CopyVerificationStatus.NeverVerified ? null : Now.AddDays(-verifiedDaysAgo),
        IsOffsite = offsite,
        IsImmutableFlagged = immutable,
        EncryptionExpected = encryptionExpected,
        EncryptionConfirmed = encrypted,
        LegalHoldReference = hold
    };

    private static CopyExpectationResult Get(IReadOnlyList<CopyExpectationResult> results, string name) => results.Single(r => r.Expectation == name);

    [Fact]
    public void NoObjective_AllExpectationsUnknown_NeverMet()
    {
        var results = BackupCopyExpectationEvaluator.Evaluate(null, [Copy("A", offsite: true, immutable: true)], Now, StaleDays);

        Assert.Equal(CopyExpectationStatus.Unknown, Get(results, "Offsite copy").Status);
        Assert.Equal(CopyExpectationStatus.Unknown, Get(results, "Immutable copy").Status);
        Assert.Equal(CopyExpectationStatus.Unknown, Get(results, "Encrypted copies").Status);
    }

    [Fact]
    public void OffsiteRequired_MetOnlyByAVerifiedOffsiteCopy()
    {
        var objective = new RecoveryObjective { RequiresOffsiteCopy = true };

        var met = BackupCopyExpectationEvaluator.Evaluate(objective, [Copy("Cloud", offsite: true)], Now, StaleDays);
        var stale = BackupCopyExpectationEvaluator.Evaluate(objective, [Copy("Cloud", offsite: true, verifiedDaysAgo: 45)], Now, StaleDays);
        var none = BackupCopyExpectationEvaluator.Evaluate(objective, [Copy("Local")], Now, StaleDays);

        Assert.Equal(CopyExpectationStatus.Met, Get(met, "Offsite copy").Status);
        Assert.Equal(CopyExpectationStatus.Gap, Get(stale, "Offsite copy").Status);
        Assert.Contains("not currently verified", Get(stale, "Offsite copy").Explanation);
        Assert.Equal(CopyExpectationStatus.Gap, Get(none, "Offsite copy").Status);
    }

    [Fact]
    public void ImmutableMet_SaysItIsAFlagNotProof()
    {
        var objective = new RecoveryObjective { RequiresImmutableCopy = true };

        var result = Get(BackupCopyExpectationEvaluator.Evaluate(objective, [Copy("Vault", immutable: true)], Now, StaleDays), "Immutable copy");

        Assert.Equal(CopyExpectationStatus.Met, result.Status);
        Assert.Contains("can't confirm it", result.Explanation);
    }

    [Fact]
    public void NotRequired_IsReportedAsNotRequired()
    {
        var objective = new RecoveryObjective { RequiresOffsiteCopy = false, RequiresEncryptedCopies = false };

        var results = BackupCopyExpectationEvaluator.Evaluate(objective, [], Now, StaleDays);

        Assert.Equal(CopyExpectationStatus.NotRequired, Get(results, "Offsite copy").Status);
        Assert.Equal(CopyExpectationStatus.NotRequired, Get(results, "Encrypted copies").Status);
    }

    [Fact]
    public void EncryptionRequired_UncheckedCopy_IsUnknown_NotMet()
    {
        var objective = new RecoveryObjective { RequiresEncryptedCopies = true };

        var result = Get(BackupCopyExpectationEvaluator.Evaluate(objective, [Copy("A", encrypted: true), Copy("B")], Now, StaleDays), "Encrypted copies");

        Assert.Equal(CopyExpectationStatus.Unknown, result.Status);
        Assert.Contains("B", result.Explanation);
    }

    [Fact]
    public void EncryptionExpectedOnACopy_FoundNotEncrypted_IsAGap()
    {
        var result = Get(BackupCopyExpectationEvaluator.Evaluate(null, [Copy("A", encryptionExpected: true, encrypted: false)], Now, StaleDays), "Encrypted copies");

        Assert.Equal(CopyExpectationStatus.Gap, result.Status);
    }

    [Fact]
    public void EncryptionConfirmedOnEveryCopy_IsMet()
    {
        var objective = new RecoveryObjective { RequiresEncryptedCopies = true };

        var result = Get(BackupCopyExpectationEvaluator.Evaluate(objective, [Copy("A", encrypted: true), Copy("B", encrypted: true)], Now, StaleDays), "Encrypted copies");

        Assert.Equal(CopyExpectationStatus.Met, result.Status);
    }

    [Fact]
    public void LegalHold_MissingCopy_IsAGap_UnverifiedIsUnknown_VerifiedIsMet()
    {
        var missing = Get(BackupCopyExpectationEvaluator.Evaluate(null, [Copy("A", CopyVerificationStatus.Missing, hold: "LH-1")], Now, StaleDays), "Legal holds");
        var unverified = Get(BackupCopyExpectationEvaluator.Evaluate(null, [Copy("A", CopyVerificationStatus.CannotVerify, hold: "LH-1")], Now, StaleDays), "Legal holds");
        var verified = Get(BackupCopyExpectationEvaluator.Evaluate(null, [Copy("A", hold: "LH-1")], Now, StaleDays), "Legal holds");
        var none = Get(BackupCopyExpectationEvaluator.Evaluate(null, [Copy("A")], Now, StaleDays), "Legal holds");

        Assert.Equal(CopyExpectationStatus.Gap, missing.Status);
        Assert.Contains("LH-1", missing.Explanation);
        Assert.Equal(CopyExpectationStatus.Unknown, unverified.Status);
        Assert.Equal(CopyExpectationStatus.Met, verified.Status);
        Assert.Equal(CopyExpectationStatus.NotRequired, none.Status);
    }
}
