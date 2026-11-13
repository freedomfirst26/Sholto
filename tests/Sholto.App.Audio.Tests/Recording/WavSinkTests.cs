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
    public void Never_overwrites_a_file_already_there()
    {
        var path = NewPath();
        File.WriteAllText(path, "keep");

        Assert.Throws<IOException>(() => new WavSinkFactory().Create(path));
        Assert.Equal("keep", File.ReadAllText(path));
    }
}
