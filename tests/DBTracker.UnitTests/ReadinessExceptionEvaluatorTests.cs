using DBTracker.Core.Entities;
using DBTracker.Core.Enums;
using DBTracker.Core.Services;
using Xunit;

namespace DBTracker.UnitTests;

public class ReadinessExceptionEvaluatorTests
{
    private static readonly DateTime Now = new(2026, 11, 10, 0, 0, 0, DateTimeKind.Utc);

    private static ReadinessException Approved(DateTime expiresAtUtc) => new()
    {
        Status = ReadinessExceptionStatus.Approved,
        ExpiresAtUtc = expiresAtUtc
    };

    [Fact]
    public void PendingApproval_EffectiveStatusIsPendingApproval_NeverActive()
    {
        var exception = new ReadinessException { Status = ReadinessExceptionStatus.PendingApproval, ExpiresAtUtc = Now.AddDays(30) };

        var effective = ReadinessExceptionEvaluator.EffectiveStatus(exception, Now);

        Assert.Equal(ReadinessExceptionEffectiveStatus.PendingApproval, effective);
        Assert.False(ReadinessExceptionEvaluator.IsActive(exception, Now));
    }

    [Fact]
    public void ApprovedAndNotYetExpired_IsActive()
    {
        var exception = Approved(Now.AddDays(10));

        Assert.True(ReadinessExceptionEvaluator.IsActive(exception, Now));
        Assert.Equal(ReadinessExceptionEffectiveStatus.Active, ReadinessExceptionEvaluator.EffectiveStatus(exception, Now));
    }

    [Fact]
    public void ApprovedButExpiryHasPassed_IsExpired_NotActive_ComputedLiveWithoutAnySweep()
    {
        var exception = Approved(Now.AddDays(-1));

        Assert.False(ReadinessExceptionEvaluator.IsActive(exception, Now));
        Assert.Equal(ReadinessExceptionEffectiveStatus.Expired, ReadinessExceptionEvaluator.EffectiveStatus(exception, Now));
        // The stored Status field is untouched — expiry is purely a live computation.
        Assert.Equal(ReadinessExceptionStatus.Approved, exception.Status);
    }

    [Fact]
    public void ExpiryExactlyNow_IsAlreadyExpired()
    {
        var exception = Approved(Now);

        Assert.Equal(ReadinessExceptionEffectiveStatus.Expired, ReadinessExceptionEvaluator.EffectiveStatus(exception, Now));
    }

    [Fact]
    public void Revoked_IsNeverActive_RegardlessOfExpiry()
    {
        var exception = new ReadinessException { Status = ReadinessExceptionStatus.Revoked, ExpiresAtUtc = Now.AddDays(30) };

        Assert.False(ReadinessExceptionEvaluator.IsActive(exception, Now));
        Assert.Equal(ReadinessExceptionEffectiveStatus.Revoked, ReadinessExceptionEvaluator.EffectiveStatus(exception, Now));
    }

    [Fact]
    public void Rejected_IsNeverActive()
    {
        var exception = new ReadinessException { Status = ReadinessExceptionStatus.Rejected, ExpiresAtUtc = Now.AddDays(30) };

        Assert.Equal(ReadinessExceptionEffectiveStatus.Rejected, ReadinessExceptionEvaluator.EffectiveStatus(exception, Now));
    }

    [Fact]
    public void HasActiveExceptionFor_MatchesOnTargetTypeAndId_AndActiveStatusOnly()
    {
        var exception = Approved(Now.AddDays(10));
        exception.TargetType = "MigrationCampaign";
        exception.TargetId = 42;
        var exceptions = new[] { exception };

        Assert.True(ReadinessExceptionEvaluator.HasActiveExceptionFor(exceptions, "MigrationCampaign", 42, Now));
        Assert.False(ReadinessExceptionEvaluator.HasActiveExceptionFor(exceptions, "MigrationCampaign", 99, Now));
        Assert.False(ReadinessExceptionEvaluator.HasActiveExceptionFor(exceptions, "RehearsalRecord", 42, Now));
    }

    [Fact]
    public void HasActiveExceptionFor_ExpiredException_DoesNotMatch()
    {
        var exception = Approved(Now.AddDays(-5));
        exception.TargetType = "MigrationCampaign";
        exception.TargetId = 1;

        Assert.False(ReadinessExceptionEvaluator.HasActiveExceptionFor([exception], "MigrationCampaign", 1, Now));
    }

    [Fact]
    public void CanApprove_SeparationRequired_SameUserBlocked_DifferentUserAllowed()
    {
        var exception = new ReadinessException { RequiresSeparateApprover = true, PreparedByUserId = "u1" };

        Assert.False(ReadinessExceptionEvaluator.CanApprove(exception, "u1"));
        Assert.True(ReadinessExceptionEvaluator.CanApprove(exception, "u2"));
    }

    [Fact]
    public void CanApprove_SeparationNotRequired_SameUserAllowed()
    {
        var exception = new ReadinessException { RequiresSeparateApprover = false, PreparedByUserId = "u1" };

        Assert.True(ReadinessExceptionEvaluator.CanApprove(exception, "u1"));
    }
}
