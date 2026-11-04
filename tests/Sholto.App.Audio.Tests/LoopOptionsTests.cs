namespace Sholto.App.Audio.Tests;

public class LoopOptionsTests
{
    [Fact]
    public void Defaults_are_beat_snap_and_a_256_frame_fade()
    {
        var options = new LoopOptions();
        Assert.Equal("beat-snap", options.Mode);
        Assert.Equal(256, options.FadeMs * 48, 6);
    }
}
