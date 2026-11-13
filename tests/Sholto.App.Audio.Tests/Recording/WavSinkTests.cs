using NAudio.Wave;
using Sholto.App.Audio.Recording;

namespace Sholto.App.Audio.Tests.Recording;

public sealed class WavSinkTests : IDisposable
{
    private const int Frames = 480;

    private readonly TempFolder _folder = new();

    public WavSinkTests() => Directory.CreateDirectory(_folder.Path);

    public void Dispose() => _folder.Dispose();

    private static float[] Ramp()
    {
        var samples = new float[Frames * 2];
        for (int i = 0; i < samples.Length; i++) samples[i] = i / (float)samples.Length - 0.5f;
        return samples;
    }

    private string NewPath() => Path.Combine(_folder.Path, "take.wav");

    [Fact]
    public void WritesReadableFloatWav()
    {
        var path = NewPath();
        var samples = Ramp();
        using (var sink = new WavSinkFactory().Create(path)) sink.Write(samples);

        using var reader = new WaveFileReader(path);
        Assert.Equal(48000, reader.WaveFormat.SampleRate);
        Assert.Equal(2, reader.WaveFormat.Channels);
        Assert.Equal(WaveFormatEncoding.IeeeFloat, reader.WaveFormat.Encoding);
        Assert.Equal(32, reader.WaveFormat.BitsPerSample);
        Assert.Equal(Frames, reader.SampleCount);
        var read = new List<float>();
        while (reader.ReadNextSampleFrame() is { } frame) read.AddRange(frame);
        Assert.Equal(samples, read);
    }

    [Fact]
    public void FlushedFileIsReadableBeforeDispose()
    {
        var path = NewPath();
        using var sink = new WavSinkFactory().Create(path);
        sink.Write(Ramp());
        sink.Flush();

        var copy = Path.Combine(_folder.Path, "copy.wav");
        using (var open = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite))
        using (var target = File.Create(copy)) open.CopyTo(target);

        using var reader = new WaveFileReader(copy);
        Assert.Equal(Frames, reader.SampleCount);
    }

    [Fact]
    public void A_full_disk_stops_the_writer_and_keeps_what_was_written_playable()
    {
        var path = NewPath();
        // Room for the header and about 100 frames of 8 bytes; then the disk is full.
        var sink = new WavSink(new DiskFullStream(new FileStream(path, FileMode.CreateNew, FileAccess.ReadWrite), 100 + 100 * 8));
        var ring = new StereoSampleRing(Frames);
        var samples = Ramp();
        ring.TryWrite(samples, 2);
        // About 50 frames a pass: the first passes fit, a later one does not.
        var writer = new MixRecordingWriter(ring, sink, TimeSpan.FromMilliseconds(50.0 / 48), 1_000_000);

        writer.Start();
        Assert.True(writer.StopAndWait(TimeSpan.FromSeconds(30)));

        Assert.Equal(DiskFullStream.DiskFullMessage, writer.FaultReason);
        using var reader = new WaveFileReader(path);
        Assert.Equal(WaveFormatEncoding.IeeeFloat, reader.WaveFormat.Encoding);
        var kept = (int)reader.SampleCount;
        Assert.InRange(kept, 1, Frames - 1);
        var read = new List<float>();
        while (reader.ReadNextSampleFrame() is { } frame) read.AddRange(frame);
        Assert.Equal(samples[..(kept * 2)], read);
    }

    [Fact]
    public void Never_overwrites_a_file_already_there()
    {
        var path = NewPath();
        File.WriteAllText(path, "keep");

        Assert.Throws<IOException>(() => new WavSinkFactory().Create(path));
        Assert.Equal("keep", File.ReadAllText(path));
    }
}
