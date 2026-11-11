using DBTracker.Tray.Core;

namespace DBTracker.Tray.Tests;

public class HealthClassifierTests
{
    private static HealthState Classify(HealthProbe probe) => HealthClassifier.Classify(probe, 8150);

    [Fact]
    public void NoResponse_IsRed()
    {
        var state = Classify(HealthProbe.Failed("timeout"));
        Assert.Equal(StatusLight.Red, state.Light);
        Assert.Contains("8150", state.Text);
    }

    [Fact]
    public void ServerError_IsRed()
    {
        Assert.Equal(StatusLight.Red, Classify(HealthProbe.Response(503, "")).Light);
    }

    [Fact]
    public void Ok_IsGreen()
    {
        var state = Classify(HealthProbe.Response(200, """{"status":"ok","installed":true,"database":"ok","version":"1.0.0"}"""));
        Assert.Equal(StatusLight.Green, state.Light);
        Assert.False(state.SetupRequired);
    }

    [Fact]
    public void SetupRequired_IsAmber_AndFlagged()
    {
        var state = Classify(HealthProbe.Response(200, """{"status":"setup-required","installed":false,"database":"not-configured","version":"1.0.0"}"""));
        Assert.Equal(StatusLight.Amber, state.Light);
        Assert.True(state.SetupRequired);
    }

    [Fact]
    public void DegradedWithUnreachableDatabase_IsAmber_AndSaysSo()
    {
        var state = Classify(HealthProbe.Response(200, """{"status":"degraded","installed":true,"database":"unreachable","version":"1.0.0"}"""));
        Assert.Equal(StatusLight.Amber, state.Light);
        Assert.Contains("database", state.Text);
    }

    [Fact]
    public void OlderVersionWithoutHealthEndpoint_IsAmber_NotRed()
    {
        Assert.Equal(StatusLight.Amber, Classify(HealthProbe.Response(404, "")).Light);
    }

    [Theory]
    [InlineData("not json")]
    [InlineData("")]
    [InlineData("""{"status":"exploded"}""")]
    public void UnreadableOrUnknownReply_IsRed(string body)
    {
        Assert.Equal(StatusLight.Red, Classify(HealthProbe.Response(200, body)).Light);
    }
}
