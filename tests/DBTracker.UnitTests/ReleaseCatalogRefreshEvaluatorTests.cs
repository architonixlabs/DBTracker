using DBTracker.Core.Entities;
using DBTracker.Core.Services;
using Xunit;

namespace DBTracker.UnitTests;

public class ReleaseCatalogRefreshEvaluatorTests
{
    [Fact]
    public void NoSourceUrl_Fails()
    {
        var outcome = ReleaseCatalogRefreshEvaluator.Evaluate(new ReleaseCatalogEntry());
        Assert.False(outcome.Succeeded);
        Assert.NotEmpty(outcome.Error);
    }

    [Fact]
    public void SourceUrlConfigured_Succeeds()
    {
        var outcome = ReleaseCatalogRefreshEvaluator.Evaluate(new ReleaseCatalogEntry { SourceUrl = "https://example.test/releases" });
        Assert.True(outcome.Succeeded);
        Assert.Empty(outcome.Error);
    }

    [Fact]
    public void ExpiredOverride_FailsAndNeedsReReview()
    {
        var entry = new ReleaseCatalogEntry { SourceUrl = "https://example.test", OverrideExpiresUtc = DateTime.UtcNow.AddDays(-1) };
        var outcome = ReleaseCatalogRefreshEvaluator.Evaluate(entry);
        Assert.False(outcome.Succeeded);
        Assert.Contains("re-review", outcome.Error);
    }

    [Fact]
    public void UnexpiredOverride_StillSucceeds()
    {
        var entry = new ReleaseCatalogEntry { SourceUrl = "https://example.test", OverrideExpiresUtc = DateTime.UtcNow.AddDays(30) };
        var outcome = ReleaseCatalogRefreshEvaluator.Evaluate(entry);
        Assert.True(outcome.Succeeded);
    }
}
