using DBTracker.Core.Services;
using Xunit;

namespace DBTracker.UnitTests;

public class DatabaseReadinessGateTests
{
    [Theory]
    [InlineData("/healthz")]
    [InlineData("/_framework/blazor.web.js")]
    [InlineData("/fonts/inter-latin.woff2")]
    [InlineData("/lib/bootstrap/css/bootstrap.min.css")]
    [InlineData("/app.css")]
    [InlineData("/favicon.png")]
    public void HealthAndStaticFiles_StayOpen(string path) =>
        Assert.True(DatabaseReadinessGate.Allows(path));

    [Theory]
    [InlineData("/")]
    [InlineData("/instances")]
    [InlineData("/Account/Login")]
    [InlineData("/api/tray/summary")]
    [InlineData("/healthzz")]
    [InlineData(null)]
    public void Pages_AreHeldUntilTheDatabaseAnswers(string? path) =>
        Assert.False(DatabaseReadinessGate.Allows(path));

    [Fact]
    public void StartupBackoff_IsShortAndCapped()
    {
        Assert.Equal(TimeSpan.FromSeconds(2), DatabaseReadinessGate.StartupBackoff(1));
        Assert.Equal(TimeSpan.FromSeconds(8), DatabaseReadinessGate.StartupBackoff(3));
        Assert.Equal(TimeSpan.FromSeconds(8), DatabaseReadinessGate.StartupBackoff(9));
    }
}
