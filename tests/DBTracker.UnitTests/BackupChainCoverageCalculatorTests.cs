using DBTracker.Core.Entities;
using DBTracker.Core.Enums;
using DBTracker.Core.Services;
using Xunit;

namespace DBTracker.UnitTests;

public class BackupChainCoverageCalculatorTests
{
    private static readonly DateTime Now = new(2026, 11, 10, 12, 0, 0, DateTimeKind.Utc);

    private static BackupChainRequirement Req(ChainMemberType type, bool required = true) => new() { MemberType = type, IsRequired = required };

    private static BackupRecord Rec(ChainMemberType? role, BackupType type, DateTime ts, BackupStatus status = BackupStatus.Success) =>
        new() { Type = type, ChainRole = role, TimestampUtc = ts, Status = status };

    [Fact]
    public void NoRequirementsRecorded_IsUnknown_NotFalseComplete()
    {
        var result = BackupChainCoverageCalculator.Compute([], [], Now, 24);

        Assert.Equal(ChainCoverageStatus.Unknown, result.Status);
        Assert.Null(result.RecoverablePointUtc);
        Assert.NotEmpty(result.MissingReasons);
    }

    [Fact]
    public void BaseAndLogBothFresh_IsComplete_RecoverablePointIsLatestLog()
    {
        var requirements = new[] { Req(ChainMemberType.Base), Req(ChainMemberType.TransactionLog) };
        var records = new[]
        {
            Rec(null, BackupType.Full, Now.AddHours(-20)),
            Rec(null, BackupType.TransactionLog, Now.AddHours(-1))
        };

        var result = BackupChainCoverageCalculator.Compute(requirements, records, Now, 24);

        Assert.Equal(ChainCoverageStatus.Complete, result.Status);
        Assert.Equal(Now.AddHours(-1), result.RecoverablePointUtc);
        Assert.Empty(result.MissingReasons);
    }

    [Fact]
    public void FreshLogOnly_WithoutBase_NeverImpliesRecoverability_StaysIncompleteWithNullPoint()
    {
        var requirements = new[] { Req(ChainMemberType.Base), Req(ChainMemberType.TransactionLog) };
        var records = new[] { Rec(null, BackupType.TransactionLog, Now.AddMinutes(-5)) };

        var result = BackupChainCoverageCalculator.Compute(requirements, records, Now, 24);

        Assert.Equal(ChainCoverageStatus.Incomplete, result.Status);
        Assert.Null(result.RecoverablePointUtc);
        Assert.Contains(result.MissingReasons, r => r.Contains("Base"));
    }

    [Fact]
    public void BaseSatisfied_LogMissing_IsIncomplete_ButRecoverableUpToBase()
    {
        var requirements = new[] { Req(ChainMemberType.Base), Req(ChainMemberType.TransactionLog) };
        var baseTime = Now.AddHours(-10);
        var records = new[] { Rec(null, BackupType.Full, baseTime) };

        var result = BackupChainCoverageCalculator.Compute(requirements, records, Now, 24);

        Assert.Equal(ChainCoverageStatus.Incomplete, result.Status);
        Assert.Equal(baseTime, result.RecoverablePointUtc);
        Assert.Contains(result.MissingReasons, r => r.Contains("TransactionLog"));
    }

    [Fact]
    public void StaleBase_OlderThanMaxAge_IsNotSatisfied()
    {
        var requirements = new[] { Req(ChainMemberType.Base) };
        var records = new[] { Rec(null, BackupType.Full, Now.AddHours(-48)) };

        var result = BackupChainCoverageCalculator.Compute(requirements, records, Now, 24);

        Assert.Equal(ChainCoverageStatus.Incomplete, result.Status);
        Assert.Null(result.RecoverablePointUtc);
    }

    [Fact]
    public void WalArchiveRole_UsesExplicitChainRole_NotBackupType()
    {
        var requirements = new[] { Req(ChainMemberType.Base), Req(ChainMemberType.WalArchive) };
        var records = new[]
        {
            Rec(null, BackupType.Full, Now.AddHours(-20)),
            Rec(ChainMemberType.WalArchive, BackupType.TransactionLog, Now.AddMinutes(-10))
        };

        var result = BackupChainCoverageCalculator.Compute(requirements, records, Now, 24);

        Assert.Equal(ChainCoverageStatus.Complete, result.Status);
        Assert.Contains(result.Members, m => m.MemberType == ChainMemberType.WalArchive && m.Satisfied);
    }

    [Fact]
    public void FailedBackupRecord_IsNotCountedAsEvidence()
    {
        var requirements = new[] { Req(ChainMemberType.Base) };
        var records = new[] { Rec(null, BackupType.Full, Now.AddHours(-1), BackupStatus.Failed) };

        var result = BackupChainCoverageCalculator.Compute(requirements, records, Now, 24);

        Assert.Equal(ChainCoverageStatus.Incomplete, result.Status);
    }

    [Fact]
    public void FutureDatedRecord_IsIgnored()
    {
        var requirements = new[] { Req(ChainMemberType.Base) };
        var records = new[] { Rec(null, BackupType.Full, Now.AddHours(5)) };

        var result = BackupChainCoverageCalculator.Compute(requirements, records, Now, 24);

        Assert.Equal(ChainCoverageStatus.Incomplete, result.Status);
    }

    [Fact]
    public void RequirementNotMarkedIsRequired_IsIgnoredEntirely()
    {
        var requirements = new[] { Req(ChainMemberType.Base), Req(ChainMemberType.Archival, required: false) };
        var records = new[] { Rec(null, BackupType.Full, Now.AddHours(-1)) };

        var result = BackupChainCoverageCalculator.Compute(requirements, records, Now, 24);

        Assert.Equal(ChainCoverageStatus.Complete, result.Status);
        Assert.DoesNotContain(result.Members, m => m.MemberType == ChainMemberType.Archival);
    }

    [Fact]
    public void PerRequirementMaxAgeOverride_IsRespected()
    {
        var requirement = Req(ChainMemberType.Base);
        requirement.MaxAgeHoursOverride = 2;
        var records = new[] { Rec(null, BackupType.Full, Now.AddHours(-3)) };

        var result = BackupChainCoverageCalculator.Compute([requirement], records, Now, 24);

        Assert.Equal(ChainCoverageStatus.Incomplete, result.Status);
    }
}
