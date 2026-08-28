using System.Threading;
using SoundFlow.Enums;
using SoundFlow.Interfaces;
using SoundFlow.Metadata.Models;
using SoundFlow.Structs;

namespace Sholto.Audio;

/// <summary>
/// <see cref="ISoundDataProvider"/> over a single in-memory interleaved-stereo
/// float buffer that supports signed vinyl-style speed — the raw-path
/// equivalent of <see cref="StemMixDataProvider"/>'s varispeed read loop,
/// stripped down to one buffer / no stem gains / no loop wrap.
///
/// Exists because SoundFlow's own <c>RawDataProvider</c> (used for
/// <see cref="Deck.Load"/>, the in-memory-but-pre-stems path) is <c>sealed</c>
/// in every method that matters (all its interface members are
/// <c>virtual sealed</c>) — there's no way to hang a speed knob off it. This
/// class replaces it on that load path so the top platter can scratch a
/// track before Demucs stems land, not just after.
/// </summary>
public sealed class ScratchDataProvider : ISoundDataProvider, IVarispeedProvider
{
    // Not readonly: Dispose() nulls this out so the (up to ~92 MB) buffer can
    // be reclaimed by GC even if something else briefly holds a reference to
    // the provider itself. Mirrors StemMixDataProvider's Dispose contract.
    private float[]? _buffer;
    private readonly int _length;   // total interleaved-sample length (always even — stereo)
    private double _position;       // fractional source position in interleaved samples

    // Signed playback rate: 1.0 = unity forward, 0 = held, negative = reverse.
    // See StemMixDataProvider's identical field for the full rationale — the
    // two providers are read the same way by Deck via IVarispeedProvider.
    private float _speed = 1f;
    public void SetSpeed(float speed) => Volatile.Write(ref _speed, speed);

    // Declick on Seek — identical scheme to StemMixDataProvider (see its
    // comment): crossfade from the last emitted sample into the new audio
    // over a few ms so a jog-driven Seek doesn't click.
    private const int FadeFrames = 256;
    private int _fadeRemaining;
    private float _lastOutL, _lastOutR;

    public ScratchDataProvider(float[] stereoSamples, int sampleRate)
    {
        if (stereoSamples is null) throw new ArgumentNullException(nameof(stereoSamples));
        _buffer = stereoSamples;
        _length = stereoSamples.Length;
        SampleRate = sampleRate;
    }

    // — ISoundDataProvider —

    public int Position => (int)Volatile.Read(ref _position);
    public int Length => _length;
    public bool CanSeek => true;
    public SampleFormat SampleFormat => SampleFormat.F32;
    public int SampleRate { get; set; }
    public bool IsDisposed { get; private set; }
    public SoundFormatInfo? FormatInfo => null;

    public event EventHandler<EventArgs>? EndOfStreamReached;
    public event EventHandler<PositionChangedEventArgs>? PositionChanged;

    public int ReadBytes(Span<float> buffer)
    {
        float speed = Volatile.Read(ref _speed);
        var samples = _buffer;
        if (samples is null) return 0;

        int outFrames = buffer.Length / 2;
        int maxSrcFrame = (_length / 2) - 2; // room for [n] and [n+1] interpolation
        double srcFrame = Volatile.Read(ref _position) / 2.0;
        int written;

        if (speed < 0f)
        {
            // Reverse (platter scratch backspin). Clamp + silence at the
            // start of the track instead of signalling end-of-stream — see
            // StemMixDataProvider.ReadChunk for the full rationale (a real
            // record runs out of groove and goes quiet, it doesn't stop the
            // deck while the user is still holding the platter).
            int of = 0;
            for (; of < outFrames; of++)
            {
                if (srcFrame <= 0.0) break;
                int iFrame = (int)Math.Floor(srcFrame);
                if (iFrame >= maxSrcFrame) iFrame = maxSrcFrame - 1;
                float frac = (float)(srcFrame - iFrame);
                WriteInterpolatedFrame(buffer, of, iFrame, frac, samples);
                srcFrame += speed;
            }
            if (of < outFrames)
            {
                buffer.Slice(of * 2, (outFrames - of) * 2).Clear();
                srcFrame = 0.0;
                of = outFrames;
            }
            written = of;
        }
        else
        {
            // Forward (unity playback, tempo fader, or a forward scratch
            // fling). speed == 0 re-emits the same frame every iteration —
            // the "parked platter" sound — and still returns a full chunk.
            int of = 0;
            for (; of < outFrames; of++)
            {
                if (srcFrame >= maxSrcFrame) break;
                int iFrame = (int)Math.Floor(srcFrame);
                float frac = (float)(srcFrame - iFrame);
                WriteInterpolatedFrame(buffer, of, iFrame, frac, samples);
                srcFrame += speed;
            }
            written = of;
        }

        Volatile.Write(ref _position, srcFrame * 2.0);

        int producedSamples = written * 2;
        if (written == 0) EndOfStreamReached?.Invoke(this, EventArgs.Empty);
        ApplyFadeIn(buffer[..producedSamples]);
        return producedSamples;
    }

    /// <summary>Linear-interpolate one output frame from source frame
    /// <paramref name="iFrame"/> / <paramref name="iFrame"/>+1 at fractional
    /// position <paramref name="frac"/>. Same math as
    /// StemMixDataProvider.WriteInterpolatedFrame, minus the per-stem gains
    /// (there's only one buffer here).</summary>
    private static void WriteInterpolatedFrame(Span<float> outBuf, int frameIndex, int iFrame, float frac, float[] samples)
    {
        int idx = iFrame * 2;
        float la = samples[idx],     lb = samples[idx + 2];
        float ra = samples[idx + 1], rb = samples[idx + 3];
        int o = frameIndex * 2;
        outBuf[o]     = la + frac * (lb - la);
        outBuf[o + 1] = ra + frac * (rb - ra);
    }

    public void Seek(int offset)
    {
        var clamped = Math.Clamp(offset, 0, _length);
        Volatile.Write(ref _position, (double)clamped);
        Volatile.Write(ref _fadeRemaining, FadeFrames);
        PositionChanged?.Invoke(this, new PositionChangedEventArgs(clamped));
    }

    /// <summary>Crossfade out of the last output sample into the new audio
    /// whenever a Seek has armed the fade counter. Identical scheme to
    /// StemMixDataProvider.ApplyFadeIn.</summary>
    private void ApplyFadeIn(Span<float> samples)
    {
        int frames = samples.Length / 2;
        if (frames == 0) return;

        int fade = Volatile.Read(ref _fadeRemaining);
        if (fade > 0)
        {
            float lastL = _lastOutL, lastR = _lastOutR;
            for (int f = 0; f < frames && fade > 0; f++)
            {
                float t = 1f - (float)fade / FadeFrames;
                samples[f * 2]     = lastL + (samples[f * 2]     - lastL) * t;
                samples[f * 2 + 1] = lastR + (samples[f * 2 + 1] - lastR) * t;
                fade--;
            }
            Volatile.Write(ref _fadeRemaining, fade);
        }

        _lastOutL = samples[(frames - 1) * 2];
        _lastOutR = samples[(frames - 1) * 2 + 1];
    }

    public void Dispose()
    {
        IsDisposed = true;
        Volatile.Write(ref _buffer, null);
    }
}
