using System.Collections.Concurrent;

namespace Sholto.Audio;

/// <summary>
/// Optional debug recorder for the stem-mix output. Enabled when the env var
/// <c>OPENDJ_LOOP_DEBUG_WAV</c> is set to a writeable path: every buffer the
/// <see cref="StemMixDataProvider"/> emits is appended to a 32-bit float
/// stereo WAV at <c>AudioFileDecoder.TargetSampleRate</c>. Drained on a
/// background thread so the audio callback never blocks on file I/O.
///
/// Capture the WAV during a "weird-sounding loop" passage and analyse it
/// offline — sample-level diffs across the loop seam reveal whether the
/// artefact is a value discontinuity, a slope kink, missing samples, or
/// something downstream of this provider entirely.
/// </summary>
public static class LoopDebug
{
    private static FileStream? _stream;
    private static BinaryWriter? _writer;
    private static long _dataSamples;     // total floats written (not frames, not bytes)
    private static readonly ConcurrentQueue<float[]> _queue = new();
    private static Thread? _writerThread;
    private static volatile bool _running;

    /// <summary>True if the env var is set and the WAV file opened OK.</summary>
    public static bool Enabled { get; private set; }

    /// <summary>The resolved output path, for logging.</summary>
    public static string? Path { get; private set; }

    static LoopDebug()
    {
        var path = Environment.GetEnvironmentVariable("OPENDJ_LOOP_DEBUG_WAV");
        if (string.IsNullOrEmpty(path)) return;
        try
        {
            _stream = File.Open(path, FileMode.Create, FileAccess.Write);
            _writer = new BinaryWriter(_stream);
            WriteWavHeaderPlaceholder(_writer);
            _running = true;
            _writerThread = new Thread(WriterLoop)
            {
                IsBackground = true,
                Name = "LoopDebug",
            };
            _writerThread.Start();
            Path = path;
            Enabled = true;
            Console.WriteLine($"[LoopDebug] recording stem-mix output to {path}");
            AppDomain.CurrentDomain.ProcessExit += (_, _) => Close();
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[LoopDebug] failed to open '{path}': {ex.Message}");
        }
    }

    /// <summary>Append a buffer of interleaved stereo float samples. Called from
    /// the audio thread; copies to a managed array and enqueues for a background
    /// writer so this is allocation+enqueue (no I/O).</summary>
    public static void Append(ReadOnlySpan<float> samples)
    {
        if (!Enabled || samples.Length == 0) return;
        var copy = new float[samples.Length];
        samples.CopyTo(copy);
        _queue.Enqueue(copy);
    }

    private static void WriterLoop()
    {
        while (_running)
        {
            Drain();
            Thread.Sleep(50);
        }
        Drain();
    }

    private static void Drain()
    {
        while (_queue.TryDequeue(out var chunk))
        {
            if (_writer is null) continue;
            // BinaryWriter.Write(float[]) doesn't exist; loop is fine — we're
            // off the audio thread and the chunks are small relative to disk.
            foreach (var s in chunk) _writer.Write(s);
            _dataSamples += chunk.Length;
        }
    }

    public static void Close()
    {
        if (!Enabled) return;
        Enabled = false;
        _running = false;
        try { _writerThread?.Join(500); } catch { }
        Drain();
        try
        {
            if (_writer is not null && _stream is not null)
            {
                PatchWavHeader(_stream, _dataSamples);
                _writer.Dispose();
                _stream.Dispose();
            }
            Console.WriteLine($"[LoopDebug] closed {Path} ({_dataSamples} samples ≈ {_dataSamples / 2.0 / AudioFileDecoder.TargetSampleRate:F1}s)");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[LoopDebug] close failed: {ex.Message}");
        }
    }

    // WAV / RIFF header for IEEE-float stereo, sample rate from
    // AudioFileDecoder.TargetSampleRate. We write 0xFFFFFFFF for the size
    // fields up front and patch them on Close() once we know the real length.
    private const ushort FormatIeeeFloat = 0x0003;
    private const ushort Channels = 2;
    private const ushort BitsPerSample = 32;

    private static void WriteWavHeaderPlaceholder(BinaryWriter w)
    {
        int sampleRate = AudioFileDecoder.TargetSampleRate;
        int byteRate = sampleRate * Channels * (BitsPerSample / 8);
        ushort blockAlign = (ushort)(Channels * (BitsPerSample / 8));

        w.Write(System.Text.Encoding.ASCII.GetBytes("RIFF"));
        w.Write((uint)0); // RIFF chunk size — patched
        w.Write(System.Text.Encoding.ASCII.GetBytes("WAVE"));
        w.Write(System.Text.Encoding.ASCII.GetBytes("fmt "));
        w.Write((uint)16);                   // fmt chunk size
        w.Write(FormatIeeeFloat);
        w.Write(Channels);
        w.Write((uint)sampleRate);
        w.Write((uint)byteRate);
        w.Write(blockAlign);
        w.Write(BitsPerSample);
        w.Write(System.Text.Encoding.ASCII.GetBytes("data"));
        w.Write((uint)0); // data chunk size — patched
    }

    private static void PatchWavHeader(FileStream s, long dataSamples)
    {
        long dataBytes = dataSamples * (BitsPerSample / 8);
        long fileSize = 44 + dataBytes; // header is 44 bytes for this layout

        // RIFF chunk size = file size - 8.
        s.Seek(4, SeekOrigin.Begin);
        Span<byte> buf = stackalloc byte[4];
        BitConverter.TryWriteBytes(buf, (uint)(fileSize - 8));
        s.Write(buf);

        // data chunk size — 4 bytes back from the start of audio data.
        s.Seek(40, SeekOrigin.Begin);
        BitConverter.TryWriteBytes(buf, (uint)dataBytes);
        s.Write(buf);
    }
}
