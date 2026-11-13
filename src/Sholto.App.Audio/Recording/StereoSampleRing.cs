namespace Sholto.App.Audio.Recording;

/// <summary>See <see cref="IStereoSampleRing"/>. One producer and one consumer, no locks: each side owns one index
/// and publishes it with <see cref="Volatile.Write(ref long, long)"/> after its copy, and reads the other's with
/// <see cref="Volatile.Read(ref long)"/>. Both indices count frames since the ring was built and only grow.</summary>
public sealed class StereoSampleRing(int capacityFrames) : IStereoSampleRing
{
    /// <summary>Floats per stored frame: the master's left and right.</summary>
    private const int StereoChannels = 2;

    private readonly int _capacityFrames = capacityFrames;
    private readonly float[] _samples = new float[capacityFrames * StereoChannels];
    private long _writtenFrames;
    private long _readFrames;

    public int AvailableFrames => (int)(Volatile.Read(ref _writtenFrames) - Volatile.Read(ref _readFrames));

    public bool TryWrite(ReadOnlySpan<float> interleaved, int channels)
    {
        // A buffer with fewer than two channels carries no stereo master to copy.
        if (channels < StereoChannels) return false;
        int frames = interleaved.Length / channels;
        long written = _writtenFrames; // only this side writes it
        long free = _capacityFrames - (written - Volatile.Read(ref _readFrames));
        if (frames > free) return false;

        int slot = (int)(written % _capacityFrames);
        for (int frame = 0; frame < frames; frame++)
        {
            int source = frame * channels;
            _samples[slot * StereoChannels] = interleaved[source];
            _samples[slot * StereoChannels + 1] = interleaved[source + 1];
            if (++slot == _capacityFrames) slot = 0;
        }
        Volatile.Write(ref _writtenFrames, written + frames);
        return true;
    }

    public int Read(Span<float> stereoDest)
    {
        long read = _readFrames; // only this side writes it
        long available = Volatile.Read(ref _writtenFrames) - read;
        int frames = (int)Math.Min(available, stereoDest.Length / StereoChannels);

        int slot = (int)(read % _capacityFrames);
        for (int frame = 0; frame < frames; frame++)
        {
            stereoDest[frame * StereoChannels] = _samples[slot * StereoChannels];
            stereoDest[frame * StereoChannels + 1] = _samples[slot * StereoChannels + 1];
            if (++slot == _capacityFrames) slot = 0;
        }
        Volatile.Write(ref _readFrames, read + frames);
        return frames * StereoChannels;
    }
}
