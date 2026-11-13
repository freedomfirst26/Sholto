using Sholto.App.Audio.Recording;

namespace Sholto.App.Audio.Tests.Recording;

public class MixRecordingOptionsTests
{
    [Fact]
    public void Defaults()
    {
        var options = new MixRecordingOptions();

        Assert.Equal("", options.Folder);
        Assert.Equal(10, options.RingSeconds);
        Assert.Equal(50, options.DrainIntervalMs);
        Assert.Equal(5, options.FlushIntervalSeconds);
        Assert.Equal(3000, options.StopTimeoutMs);
    }
}
