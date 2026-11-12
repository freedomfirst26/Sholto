namespace Sholto.App.Tests;

/// <summary>The magnet's snap thresholds and bar window keep the values they had as consts in
/// <c>MagnetSnap</c>.</summary>
public class MagnetismOptionsDefaultsTests
{
    [Fact]
    public void Snap_thresholds_and_bar_window_are_pinned()
    {
        var options = new MagnetismOptions();

        Assert.Equal(0.3, options.EngageThreshold);
        Assert.Equal(0.15, options.DisengageThreshold);
        Assert.Equal(0.15, options.BarWindowSeconds);
    }

    [Fact]
    public void Existing_defaults_are_pinned()
    {
        var options = new MagnetismOptions();

        Assert.Equal(0.01, options.BpmEligibilityTolerance);
        Assert.Equal(TimeSpan.FromMilliseconds(180), options.JogIdleForQuantize);
        Assert.Equal(TimeSpan.FromSeconds(2), options.JogRecencyForQuantize);
    }
}
