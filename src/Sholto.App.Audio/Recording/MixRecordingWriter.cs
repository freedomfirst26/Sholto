using Sholto.App.Analysis;

namespace Sholto.App.Audio.Recording;

/// <summary>See <see cref="IMixRecordingWriter"/>. The thread reads the ring into a buffer allocated here, writes it
/// to the sink, rewrites the header after every <c>flushIntervalFrames</c> written, and sleeps
/// <c>drainInterval</c> whenever the ring is empty. A write that throws ends the thread: it records the reason,
/// closes the file (keeping what was written as a valid WAV) and exits, for the app thread to notice.</summary>
public sealed class MixRecordingWriter : IMixRecordingWriter
{
    private const int StereoChannels = 2;
    private const string ThreadName = "MixRecordingWriter";
    /// <summary>The message NAudio's <c>WaveFileWriter</c> throws with when a write would pass the RIFF size limit.</summary>
    private const string NAudioTooLargeMessage = "WAV file too large";
    private const string SizeLimitReason = "WAV size limit reached";

    private readonly IStereoSampleReader _reader;
    private readonly IWavSink _sink;
    private readonly TimeSpan _drainInterval;
    private readonly long _flushIntervalFrames;
    private readonly float[] _buffer;
    private readonly Thread _thread;
    private volatile bool _stopRequested;
    private volatile string? _faultReason;
    private bool _started;

    /// <param name="drainInterval">How long to sleep when the ring is empty; the buffer holds this much audio.</param>
    /// <param name="flushIntervalFrames">Frames written between header rewrites.</param>
    public MixRecordingWriter(IStereoSampleReader reader, IWavSink sink, TimeSpan drainInterval, long flushIntervalFrames)
    {
        _reader = reader;
        _sink = sink;
        _drainInterval = drainInterval;
        _flushIntervalFrames = flushIntervalFrames;
        int bufferFrames = Math.Max(1, (int)(drainInterval.TotalSeconds * EngineAudio.SampleRate));
        _buffer = new float[bufferFrames * StereoChannels];
        _thread = new Thread(Run) { IsBackground = true, Name = ThreadName };
    }

    public string? FaultReason => _faultReason;

    public void Start()
    {
        _thread.Start();
        _started = true;
    }

    public bool StopAndWait(TimeSpan timeout)
    {
        _stopRequested = true;
        if (_started && !_thread.Join(timeout)) return false;
        // A faulted thread has already closed the file.
        if (_faultReason is null) _sink.Dispose();
        return true;
    }

    private void Run()
    {
        try
        {
            Drain();
        }
        // The thread's one way out on failure: the reason goes to the app thread, which stops the recording and
        // publishes it; nothing is swallowed. Disk full, an unplugged drive and the size limit all land here.
        catch (Exception e)
        {
            _faultReason = Reason(e);
            try
            {
                _sink.Dispose();
            }
            // The disk itself failed: the file stays as its last header rewrite left it, still playable.
            catch (IOException)
            {
            }
        }
    }

    private void Drain()
    {
        long sinceFlush = 0;
        while (true)
        {
            // Read the flag before the ring, so a stop asked for during this pass still drains what was written.
            bool stopping = _stopRequested;
            int read = _reader.Read(_buffer);
            if (read > 0)
            {
                _sink.Write(_buffer.AsSpan(0, read));
                sinceFlush += read / StereoChannels;
                if (sinceFlush >= _flushIntervalFrames)
                {
                    _sink.Flush();
                    sinceFlush = 0;
                }
                continue;
            }
            if (stopping) return;
            Thread.Sleep(_drainInterval);
        }
    }

    private string Reason(Exception e) =>
        e is ArgumentException { Message: NAudioTooLargeMessage } ? SizeLimitReason : e.Message;
}
