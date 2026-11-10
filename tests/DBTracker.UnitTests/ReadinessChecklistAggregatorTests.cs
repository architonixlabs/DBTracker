using DBTracker.Core.Entities;
using DBTracker.Core.Enums;
using DBTracker.Core.Services;
using Xunit;

namespace DBTracker.UnitTests;

public class ReadinessChecklistAggregatorTests
{
    private static readonly DateTime Now = new(2026, 11, 10, 0, 0, 0, DateTimeKind.Utc);

    private static ReadinessCheckItem Item(ReadinessCheckStatus status = ReadinessCheckStatus.Unknown, DateTime? evidenceExpiresAtUtc = null, bool isInvalidated = false) => new()
    {
        Area = ReadinessCheckArea.DriverOrmCompatibility,
        Label = "Driver/ORM compatibility",
        Status = status,
        EvidenceExpiresAtUtc = evidenceExpiresAtUtc,
        IsInvalidated = isInvalidated
    };

    [Fact]
    public void EmptyChecklist_AllZero_NoBlockingFindings()
    {
        var summary = ReadinessChecklistAggregator.Summarize([], Now);

        Assert.Equal(0, summary.TotalCount);
        Assert.False(summary.HasBlockingFindings);
        Assert.Empty(summary.BlockingFindings);
        Assert.Empty(summary.UnknownChecks);
    }

    [Fact]
    public void FreshItem_DefaultsToUnknown_NeverPassed()
    {
        var item = new ReadinessCheckItem { Area = ReadinessCheckArea.BackupTooling, Label = "Backup tooling" };

        Assert.Equal(ReadinessCheckStatus.Unknown, item.Status);
        var summary = ReadinessChecklistAggregator.Summarize([item], Now);
        Assert.Equal(1, summary.UnknownCount);
        Assert.Equal(0, summary.PassedCount);
    }

    [Fact]
    public void FailedItem_IsBlockingAndDetailIsRetained()
    {
        var item = Item(ReadinessCheckStatus.Failed);
        item.Findings = "ODBC driver 3.x is not certified against the target edition.";
        var summary = ReadinessChecklistAggregator.Summarize([item], Now);

        Assert.True(summary.HasBlockingFindings);
        Assert.Single(summary.BlockingFindings);
        Assert.Equal(item.Findings, summary.BlockingFindings[0].Findings);
    }

    [Fact]
    public void PassedItem_WithUnexpiredEvidence_StaysPassed()
    {
        var item = Item(ReadinessCheckStatus.Passed, evidenceExpiresAtUtc: Now.AddDays(10));
        var summary = ReadinessChecklistAggregator.Summarize([item], Now);

        Assert.Equal(1, summary.PassedCount);
        Assert.Equal(0, summary.UnknownCount);
    }

    [Fact]
    public void PassedItem_WithExpiredEvidence_IsReportedUnknown_NotPassed()
    {
        var item = Item(ReadinessCheckStatus.Passed, evidenceExpiresAtUtc: Now.AddDays(-1));
        var effective = ReadinessChecklistAggregator.EffectiveStatus(item, Now);
        var summary = ReadinessChecklistAggregator.Summarize([item], Now);

        Assert.Equal(ReadinessCheckStatus.Unknown, effective);
        Assert.Equal(0, summary.PassedCount);
        Assert.Equal(1, summary.UnknownCount);
        // The original recorded status is preserved on the row itself.
        Assert.Equal(ReadinessCheckStatus.Passed, item.Status);
    }

    [Fact]
    public void InvalidatedItem_IsReportedUnknown_RegardlessOfStoredStatus()
    {
        var item = Item(ReadinessCheckStatus.Passed, isInvalidated: true);
        var effective = ReadinessChecklistAggregator.EffectiveStatus(item, Now);

        Assert.Equal(ReadinessCheckStatus.Unknown, effective);
    }

    [Fact]
    public void FailedItem_PastEvidenceExpiry_AlsoBecomesUnknown_NotCountedAsBlocking()
    {
        var item = Item(ReadinessCheckStatus.Failed, evidenceExpiresAtUtc: Now.AddDays(-5));
        var summary = ReadinessChecklistAggregator.Summarize([item], Now);

        Assert.False(summary.HasBlockingFindings);
        Assert.Equal(1, summary.UnknownCount);
    }

    [Fact]
    public void NotApplicableItem_CountsSeparately_NotPassedOrUnknown()
    {
        var item = Item(ReadinessCheckStatus.NotApplicable);
        var summary = ReadinessChecklistAggregator.Summarize([item], Now);

        Assert.Equal(1, summary.NotApplicableCount);
        Assert.Equal(0, summary.PassedCount);
        Assert.Equal(0, summary.UnknownCount);
    }

    [Fact]
    public void MixedChecklist_CountsEachBucketIndependently()
    {
        var items = new List<ReadinessCheckItem>
        {
            Item(ReadinessCheckStatus.Passed),
            Item(ReadinessCheckStatus.Failed),
            Item(ReadinessCheckStatus.Unknown),
            Item(ReadinessCheckStatus.NotApplicable),
        };
        var summary = ReadinessChecklistAggregator.Summarize(items, Now);

        Assert.Equal(4, summary.TotalCount);
        Assert.Equal(1, summary.PassedCount);
        Assert.Equal(1, summary.FailedCount);
        Assert.Equal(1, summary.UnknownCount);
        Assert.Equal(1, summary.NotApplicableCount);
        Assert.True(summary.HasBlockingFindings);
    }
}
