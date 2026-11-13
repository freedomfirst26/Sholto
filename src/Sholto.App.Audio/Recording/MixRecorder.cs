namespace Sholto.App.Audio.Recording;

/// <summary>See <see cref="IMixRecorder"/>. <see cref="Write"/> runs on the audio thread: it reads the ring once,
/// copies or drops the whole buffer, and counts it with <see cref="Interlocked"/>; it never waits for room.</summary>
public sealed class MixRecorder : IMixRecorder, IMasterTap
{
    private IStereoSampleWriter? _ring;
    private long _capturedFrames;
    private long _droppedFrames;

    public long CapturedFrames => Interlocked.Read(ref _capturedFrames);

    public long DroppedFrames => Interlocked.Read(ref _droppedFrames);

    public void Arm(IStereoSampleWriter ring)
    {
        Interlocked.Exchange(ref _capturedFrames, 0);
        Interlocked.Exchange(ref _droppedFrames, 0);
        Volatile.Write(ref _ring, ring);
    }

    public void Disarm() => Volatile.Write(ref _ring, null);

    public void Write(ReadOnlySpan<float> interleaved, int channels)
    {
        var ring = Volatile.Read(ref _ring);
        if (ring is null || channels <= 0) return;
        long frames = interleaved.Length / channels;
        if (ring.TryWrite(interleaved, channels)) Interlocked.Add(ref _capturedFrames, frames);
        else Interlocked.Add(ref _droppedFrames, frames);
    }
}
