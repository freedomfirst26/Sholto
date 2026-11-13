using Microsoft.Extensions.Options;
using NAudio.Wave;
using Sholto.App.Audio.Recording;
using Sholto.App.Recording;
using Sholto.Data;

namespace Sholto.App.Tests.Recording;

/// <summary>The real recording: toggle on, the audio thread's master buffers, toggle off, and the WAV left in the
/// folder. The buffers are fed to the recorder the way the output router hands them over, 4 channels with the cue on
/// ch3-4, and the file holds ch1-2 only.</summary>
public sealed class MixRecordingEndToEndTests : IDisposable
{
    private const int Channels = 4;
    private const int FramesPerBuffer = 960;

    private readonly string _folder = Path.Combine(Path.GetTempPath(), "sholto-rec-" + Guid.NewGuid().ToString("N"));
    private readonly CapturingPublisher _publisher = new();
    private readonly MixRecorder _recorder = new();
    private readonly MixRecording _recording;

    public MixRecordingEndToEndTests()
    {
        var options = Options.Create(new MixRecordingOptions { Folder = _folder, DrainIntervalMs = 1 });
        _recording = new MixRecording(_recorder, new StereoSampleRingFactory(options), new MixRecordingPaths(options),
            new WavSinkFactory(), new MixRecordingWriterFactory(options), _publisher, new SettableFrameClock(),
            new RecordingLogFactory(), options);
    }

    public void Dispose()
    {
        if (Directory.Exists(_folder)) Directory.Delete(_folder, recursive: true);
    }

    private void Toggle() => _recording.Handle(new ToggleMixRecording(new Origin("test", "record", "press")));

    private float[] Buffer(int index)
    {
        var buffer = new float[FramesPerBuffer * Channels];
        for (int frame = 0; frame < FramesPerBuffer; frame++)
        {
            float level = (index * FramesPerBuffer + frame) * 1e-5f;
            buffer[frame * Channels] = level;
            buffer[frame * Channels + 1] = -level;
            buffer[frame * Channels + 2] = 0.9f; // the cue, which must not reach the file
            buffer[frame * Channels + 3] = 0.9f;
        }
        return buffer;
    }

    [Fact]
    public void Toggling_on_and_off_leaves_a_float_wav_of_the_master()
    {
        Toggle();
        var expected = new List<float>();
        for (int i = 0; i < 50; i++)
        {
            var buffer = Buffer(i);
            _recorder.Write(buffer, Channels);
            for (int frame = 0; frame < FramesPerBuffer; frame++)
            {
                expected.Add(buffer[frame * Channels]);
                expected.Add(buffer[frame * Channels + 1]);
            }
        }
        Toggle();

        var stopped = Assert.Single(_publisher.Of<MixRecordingStopped>());
        Assert.Null(stopped.Reason);
        Assert.Equal(_folder, Path.GetDirectoryName(stopped.FilePath));
        Assert.Matches(@"^\d{4}-\d{2}-\d{2} \d{2}-\d{2}-\d{2}\.wav$", Path.GetFileName(stopped.FilePath));
        using var reader = new WaveFileReader(stopped.FilePath!);
        Assert.Equal(48000, reader.WaveFormat.SampleRate);
        Assert.Equal(2, reader.WaveFormat.Channels);
        Assert.Equal(WaveFormatEncoding.IeeeFloat, reader.WaveFormat.Encoding);
        Assert.Equal(32, reader.WaveFormat.BitsPerSample);
        var recorded = new List<float>();
        while (reader.ReadNextSampleFrame() is { } frame) recorded.AddRange(frame);
        Assert.Equal(expected, recorded);
    }

    [Fact]
    public void A_second_recording_in_the_same_second_gets_its_own_file()
    {
        Toggle();
        _recorder.Write(Buffer(0), Channels);
        Toggle();
        Toggle();
        _recorder.Write(Buffer(1), Channels);
        Toggle();

        var files = _publisher.Of<MixRecordingStopped>().Select(stopped => stopped.FilePath).ToList();
        Assert.Equal(2, files.Count);
        Assert.EndsWith(" (2).wav", files[1]);
        Assert.All(files, file => Assert.True(File.Exists(file)));
    }
}
