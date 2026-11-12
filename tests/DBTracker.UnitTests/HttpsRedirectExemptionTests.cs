using DBTracker.Core.Services;
using Xunit;

namespace DBTracker.UnitTests;

public class HttpsRedirectExemptionTests
{
    [Theory]
    [InlineData("/healthz")]
    [InlineData("/HEALTHZ")]
    [InlineData("/api/tray/summary")]
    public void TrayEndpoints_FromThisMachine_SkipTheRedirect(string path) =>
        Assert.True(HttpsRedirectExemption.Applies(path, isLoopback: true));

    [Theory]
    [InlineData("/healthz")]
    [InlineData("/api/tray/summary")]
    public void TrayEndpoints_FromAnotherMachine_AreStillRedirected(string path) =>
        Assert.False(HttpsRedirectExemption.Applies(path, isLoopback: false));

    [Theory]
    [InlineData("/")]
    [InlineData("/Account/Login")]
    [InlineData("/healthzzz")]
    [InlineData("/api/trayx")]
    [InlineData(null)]
    public void EverythingElse_IsRedirected_EvenFromThisMachine(string? path) =>
        Assert.False(HttpsRedirectExemption.Applies(path, isLoopback: true));
}
