using Microsoft.Extensions.Options;
using NAudio.Wave;
using Sholto.App.Audio.Recording;
using SoundFlow.Backends.MiniAudio;
using SoundFlow.Enums;
using SoundFlow.Structs;

namespace Sholto.App.Audio.Tests.Recording;

/// <summary>The audio side of a recording, real parts end to end: the router's tap, the ring, the writer thread and
/// the WAV file. The file holds exactly the master ch1-2 the router produced, never the cue on ch3-4 (the cued ramp
/// is there on a 4-channel device).</summary>
public sealed class MasterRecordingEndToEndTests : IDisposable
{
    private const int FramesPerBuffer = 960;
    private const int Buffers = 30;

    private readonly TempFolder _folder = new();

    public MasterRecordingEndToEndTests() => Directory.CreateDirectory(_folder.Path);

    public void Dispose() => _folder.Dispose();

    [Theory]
    [InlineData(2)]
    [InlineData(4)]
    public void The_file_is_the_routers_master_ch1_2(int channels)
    {
        var engine = new MiniAudioEngine();
        var stereo = new AudioFormat { SampleRate = 48000, Channels = 2, Format = SampleFormat.F32 };
        var device = new AudioFormat { SampleRate = 48000, Channels = channels, Format = SampleFormat.F32 };
        var options = Options.Create(new MixRecordingOptions { DrainIntervalMs = 1 });
        var recorder = new MixRecorder();
        ICueOutputRouterFactory routers = new CueOutputRouterFactory(Options.Create(new AudioTimingOptions()));
        // The tee keeps the master the router composed, then hands the buffer on to the recorder.
        var tee = new TeeTap(recorder);
        var router = routers.Create(engine, device, [new RampSource(engine, stereo)], false, tee);

        var path = Path.Combine(_folder.Path, "take.wav");
        var ring = new StereoSampleRingFactory(options).Create();
        var writer = new MixRecordingWriterFactory(options).Create(ring, new WavSinkFactory().Create(path));
        writer.Start();
        recorder.Arm(ring);
        var buffer = new float[FramesPerBuffer * channels];
        for (int i = 0; i < Buffers; i++)
        {
            buffer.AsSpan().Clear(); // Process adds into the buffer
            router.Process(buffer, channels);
        }
        recorder.Disarm();
        Assert.True(writer.StopAndWait(TimeSpan.FromSeconds(30)));

        using var reader = new WaveFileReader(path);
        Assert.Equal(48000, reader.WaveFormat.SampleRate);
        Assert.Equal(2, reader.WaveFormat.Channels);
        Assert.Equal(WaveFormatEncoding.IeeeFloat, reader.WaveFormat.Encoding);
        var recorded = new List<float>();
        while (reader.ReadNextSampleFrame() is { } frameSamples) recorded.AddRange(frameSamples);
        Assert.Equal(FramesPerBuffer * Buffers * 2, tee.Master.Count);
        Assert.Equal(tee.Master, recorded);
        Assert.Equal(0, recorder.DroppedFrames);
    }
}
