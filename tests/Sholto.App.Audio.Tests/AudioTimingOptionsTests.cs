namespace Sholto.App.Audio.Tests;

public class AudioTimingOptionsTests
{
    [Fact]
    public void Defaults_to_off() => Assert.False(new AudioTimingOptions().Enabled);
}
