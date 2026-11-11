using DBTracker.Tray.Core;

namespace DBTracker.Tray.Tests;

public class LightTransitionDetectorTests
{
    [Fact]
    public void FirstLight_IsBaseline_WithoutNotification()
    {
        var detector = new LightTransitionDetector();
        Assert.Null(detector.Observe(StatusLight.Green));
        Assert.Equal(StatusLight.Green, detector.Confirmed);
    }

    [Fact]
    public void SingleBlip_DoesNotNotify()
    {
        var detector = new LightTransitionDetector();
        detector.Observe(StatusLight.Green);
        Assert.Null(detector.Observe(StatusLight.Red));
        Assert.Null(detector.Observe(StatusLight.Green));
        Assert.Equal(StatusLight.Green, detector.Confirmed);
    }

    [Fact]
    public void SustainedChange_NotifiesOnce()
    {
        var detector = new LightTransitionDetector();
        detector.Observe(StatusLight.Green);
        Assert.Null(detector.Observe(StatusLight.Red));
        var transition = detector.Observe(StatusLight.Red);
        Assert.NotNull(transition);
        Assert.Equal(StatusLight.Green, transition!.From);
        Assert.Equal(StatusLight.Red, transition.To);
        Assert.Null(detector.Observe(StatusLight.Red));
    }

    [Fact]
    public void AlternatingLights_NeverConfirm()
    {
        var detector = new LightTransitionDetector();
        detector.Observe(StatusLight.Green);
        Assert.Null(detector.Observe(StatusLight.Red));
        Assert.Null(detector.Observe(StatusLight.Amber));
        Assert.Null(detector.Observe(StatusLight.Red));
        Assert.Equal(StatusLight.Green, detector.Confirmed);
    }
}
