using System.Runtime.InteropServices;
using NAudio.Wave;
using Sholto.App.Analysis;

namespace Sholto.App.Audio.Recording;

/// <summary>See <see cref="IWavSink"/>: NAudio's <see cref="WaveFileWriter"/> at the engine rate, stereo, 32-bit
/// float. It adds no size guard: <see cref="WaveFileWriter"/> already refuses a write that would pass the RIFF
/// limit, with an <see cref="ArgumentException"/>.</summary>
public sealed class WavSink : IWavSink
{
    private const int StereoChannels = 2;

    private readonly WaveFileWriter _writer;

    /// <param name="file">The new, empty file; the sink owns it and closes it on dispose.</param>
    public WavSink(Stream file)
    {
        _writer = new WaveFileWriter(file, WaveFormat.CreateIeeeFloatWaveFormat(EngineAudio.SampleRate, StereoChannels));
    }

    public void Write(ReadOnlySpan<float> stereo) => _writer.Write(MemoryMarshal.AsBytes(stereo));

    public void Flush() => _writer.Flush();

    public void Dispose() => _writer.Dispose();
}
