using Xunit;

namespace Sholto.App.Audio.Tests;

public class AudioBackendLogLineTests
{
    [Fact]
    public void NamesTheBackendTheProbeReports()
    {
        var line = new AudioBackendLogLine(new FakeAudioBackendProbe("PulseAudio"));

        Assert.Equal(
            "backend=PulseAudio; device=DDJ-FLX4 started; reported 4ch, opened 4ch — master 1-2 + headphone cue 3-4; buffer=20ms × 3",
            line.DeviceStarted("DDJ-FLX4", reportedChannels: 4, openedChannels: 4, periodMs: 20, periods: 3));
    }

    [Fact]
    public void SaysUnknownWhenTheProbeCannotTell()
    {
        var line = new AudioBackendLogLine(new FakeAudioBackendProbe(null));

        Assert.Equal(
            "backend=unknown; device=Speakers started; reported none, opened 2ch — stereo master (no cue bus); buffer=20ms × 3",
            line.DeviceStarted("Speakers", reportedChannels: 0, openedChannels: 2, periodMs: 20, periods: 3));
    }

    [Fact]
    public void AsksTheProbeWhenTheLineIsWritten_NotWhenBuilt()
    {
        var probe = new FakeAudioBackendProbe(null);
        var line = new AudioBackendLogLine(probe);
        Assert.Equal(0, probe.Reads);

        probe.Backend = "ALSA";
        string text = line.DeviceStarted("Speakers", 2, 2, 20, 3);

        Assert.Equal(1, probe.Reads);
        Assert.StartsWith("backend=ALSA; ", text);
    }
}
