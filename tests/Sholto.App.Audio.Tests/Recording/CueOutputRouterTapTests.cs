using Microsoft.Extensions.Options;
using Sholto.App.Audio.Recording;
using SoundFlow.Backends.MiniAudio;
using SoundFlow.Enums;
using SoundFlow.Structs;

namespace Sholto.App.Audio.Tests.Recording;

public class CueOutputRouterTapTests
{
    private const int Frames = 64;

    private readonly MiniAudioEngine _engine = new();
    private readonly AudioFormat _stereo = new() { SampleRate = 48000, Channels = 2, Format = SampleFormat.F32 };
    private readonly AudioFormat _device = new() { SampleRate = 48000, Channels = 4, Format = SampleFormat.F32 };
    private readonly ICueOutputRouterFactory _factory = new CueOutputRouterFactory(Options.Create(new AudioTimingOptions()));

    private CueOutputRouter Router(IMasterTap tap, float masterGain, bool cueActive) =>
        _factory.Create(_engine, _device, [new ConstantSource(_engine, _stereo, 0.5f, masterGain, cueActive)], false, tap);

    [Fact]
    public void Router_HandsMixedMasterBufferToTap()
    {
        var tap = new CapturingTap();

        Router(tap, masterGain: 1f, cueActive: false).Process(new float[Frames * 4], 4);

        var (buffer, channels) = Assert.Single(tap.Seen);
        Assert.Equal(4, channels);
        for (int frame = 0; frame < Frames; frame++)
        {
            Assert.Equal(0.5f, buffer[frame * 4]);
            Assert.Equal(0.5f, buffer[frame * 4 + 1]);
        }
    }

    [Fact]
    public void Router_TapSeesPostFaderMaster_NotCue()
    {
        var tap = new CapturingTap();

        Router(tap, masterGain: 0f, cueActive: true).Process(new float[Frames * 4], 4);

        var (buffer, _) = Assert.Single(tap.Seen);
        for (int frame = 0; frame < Frames; frame++)
        {
            Assert.Equal(0f, buffer[frame * 4]);
            Assert.Equal(0f, buffer[frame * 4 + 1]);
            Assert.Equal(0.5f, buffer[frame * 4 + 2]); // the cue is there, on ch3-4, for the phones only
        }
        // And what the recorder keeps of that buffer is the silent master.
        var ring = new StereoSampleRing(Frames);
        Assert.True(ring.TryWrite(buffer, 4));
        var kept = new float[Frames * 2];
        ring.Read(kept);
        Assert.All(kept, sample => Assert.Equal(0f, sample));
    }

    [Fact]
    public void Factory_NewOverload_PassesTap()
    {
        var tap = new CapturingTap();
        var router = _factory.Create(_engine, _device, [], masterCueActive: true, tap);

        router.Process(new float[Frames * 4], 4);

        Assert.True(router.MasterCueActive);
        Assert.Single(tap.Seen);
    }

    [Fact]
    public void The_overloads_without_a_tap_record_nothing_and_still_mix()
    {
        var buffer = new float[Frames * 2];
        var router = _factory.Create(_engine, _stereo, [new ConstantSource(_engine, _stereo, 0.5f, 1f, false)]);
        router.Volume = UnityGain.CentrePanVolume;

        router.Process(buffer, 2);

        Assert.All(buffer, sample => Assert.Equal(0.5f, sample));
    }
}
