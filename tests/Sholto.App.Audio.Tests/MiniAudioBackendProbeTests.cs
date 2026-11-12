using SoundFlow.Backends.MiniAudio;
using Xunit;

namespace Sholto.App.Audio.Tests;

public sealed class MiniAudioBackendProbeTests : IDisposable
{
    private readonly MiniAudioEngine _engine = new();

    public void Dispose() => _engine.Dispose();

    // Raw values are native miniaudio 0.11 ma_backend numbers (0-based), the ones SoundFlow 1.4.1's
    // ActiveBackend carries; its own 1-based enum labels 7 as Oss, which is the bug this pins.
    [Theory]
    [InlineData(0, "WASAPI")]
    [InlineData(6, "OSS")]
    [InlineData(7, "PulseAudio")]
    [InlineData(8, "ALSA")]
    [InlineData(9, "JACK")]
    [InlineData(14, "Null")]
    public void NamesNativeBackendValuesAsMiniaudioDoes(int maBackend, string expected)
        => Assert.Equal(expected, new MiniAudioBackendProbe(_engine).NameOf(maBackend));

    [Theory]
    [InlineData(-1)]
    [InlineData(15)]
    public void UndefinedValueIsUnknown(int maBackend)
        => Assert.Null(new MiniAudioBackendProbe(_engine).NameOf(maBackend));

    [Fact]
    public void ActiveBackendIsTheEnginesRawValueNamed()
    {
        var probe = new MiniAudioBackendProbe(_engine);

        Assert.Equal(probe.NameOf((int)_engine.ActiveBackend), probe.ActiveBackend());
    }
}
