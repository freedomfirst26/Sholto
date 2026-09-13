namespace Sholto.Bench.Rendering;

/// <summary>
/// Minimal 32-bit IEEE-float PCM WAV writer. No dependency beyond the BCL —
/// ffmpeg reads float WAV natively, and float avoids an int16 clipping/scale
/// question the measurement step would otherwise have to account for.
/// </summary>
public sealed class WavWriter : IDisposable
{
    private readonly FileStream _fs;
    private readonly BinaryWriter _bw;
    private long _dataBytes;
    private bool _disposed;

    public WavWriter(string path, int sampleRate, int channels)
    {
        _fs = File.Create(path);
        _bw = new BinaryWriter(_fs);

        _bw.Write("RIFF"u8);
        _bw.Write(0); // RIFF chunk size — patched in Dispose
        _bw.Write("WAVE"u8);

        _bw.Write("fmt "u8);
        _bw.Write(16);                              // fmt chunk size
        _bw.Write((short)3);                         // WAVE_FORMAT_IEEE_FLOAT
        _bw.Write((short)channels);
        _bw.Write(sampleRate);
        _bw.Write(sampleRate * channels * 4);        // byte rate
        _bw.Write((short)(channels * 4));             // block align
        _bw.Write((short)32);                         // bits per sample

        _bw.Write("data"u8);
        _bw.Write(0); // data chunk size — patched in Dispose
    }

    /// <summary>Appends interleaved float samples (already in output-channel order).</summary>
    public void WriteInterleaved(ReadOnlySpan<float> samples)
    {
        foreach (var s in samples) _bw.Write(s);
        _dataBytes += samples.Length * 4L;
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        _bw.Flush();
        long fileLength = _fs.Length;
        _fs.Position = 4;
        _bw.Write((int)(fileLength - 8));
        _fs.Position = 40;
        _bw.Write((int)_dataBytes);
        _bw.Flush();
        _bw.Dispose();
        _fs.Dispose();
    }
}
