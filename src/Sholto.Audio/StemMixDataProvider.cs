using System.Threading;
using SoundFlow.Enums;
using SoundFlow.Interfaces;
using SoundFlow.Metadata.Models;
using SoundFlow.Structs;

namespace Sholto.Audio;

/// <summary>
/// One <see cref="ISoundDataProvider"/> that holds the 4 decoded stem WAVs of
/// a track and mixes them on demand. The audio thread reads from a single
/// player → single resampler → single position; per-stem mute / level is a
/// lock-free <c>Volatile.Write</c> to one of four floats.
///
/// This replaces the per-deck 4-SoundPlayer arrangement that ran 4× the
/// resampling and forced the SoundFlow mixer to sum streams every buffer. With
/// this provider in front of a single <see cref="SoundFlow.Components.SoundPlayer"/>
/// stem playback costs exactly the same as single-track playback.
///
/// Layout: each stem array is interleaved stereo float32 at the decoder's
/// target sample rate (<see cref="AudioFileDecoder.TargetSampleRate"/>). All
/// four arrays are required to be the same length; the
/// constructor truncates to <c>min(lengths)</c> if they differ.
/// </summary>
public sealed class StemMixDataProvider : ISoundDataProvider
{
    public const int StemCount = 4;
    public const int Drums = 0, Vocals = 1, Bass = 2, Other = 3;

    private readonly float[][] _stems;
    private readonly int _length;            // total interleaved-sample length per stem (always even — stereo)
    private double _position;                // fractional source position in interleaved samples (sub-sample precision for vinyl-mode speed)

    // Per-stem gain. Set via SetGain from UI/MIDI threads; read once per buffer
    // by the audio thread. Volatile keeps the read/write safe across cores
    // without locks.
    private float _g0 = 1f, _g1 = 1f, _g2 = 1f, _g3 = 1f;

    // Vinyl-style speed (1.0 = unity, <1 slower & lower pitch, >1 faster & higher).
    // Set lock-free; the audio thread reads it once per buffer.
    private float _speed = 1f;
    public void SetSpeed(float speed) => Volatile.Write(ref _speed, MathF.Max(0.01f, speed));

    // Declick on Seek. Naïve "fade in from 0" produces its OWN click at the
    // start of the fade because the previous buffer ended at some non-zero
    // value. So we remember the last output sample per channel and crossfade
    // from it to the new audio over a few ms — no step at either end.
    // ~5 ms at 48 kHz is short enough to be inaudible as a gap but long
    // enough to smooth the discontinuity.
    private const int FadeFrames = 256;
    private int _fadeRemaining;
    private float _lastOutL, _lastOutR;

    public StemMixDataProvider(float[] drums, float[] vocals, float[] bass, float[] other, int sampleRate)
    {
        if (drums is null || vocals is null || bass is null || other is null)
            throw new ArgumentNullException();

        _stems = new[] { drums, vocals, bass, other };
        _length = Math.Min(Math.Min(drums.Length, vocals.Length),
                           Math.Min(bass.Length, other.Length));
        SampleRate = sampleRate;
    }

    /// <summary>Set a stem's gain (0 = mute, 1 = unity). Lock-free.</summary>
    public void SetGain(int stem, float gain)
    {
        switch (stem)
        {
            case Drums:  Volatile.Write(ref _g0, gain); break;
            case Vocals: Volatile.Write(ref _g1, gain); break;
            case Bass:   Volatile.Write(ref _g2, gain); break;
            default:     Volatile.Write(ref _g3, gain); break;
        }
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
        // Snapshot speed + gains once per buffer.
        float speed = Volatile.Read(ref _speed);
        float g0 = Volatile.Read(ref _g0);
        float g1 = Volatile.Read(ref _g1);
        float g2 = Volatile.Read(ref _g2);
        float g3 = Volatile.Read(ref _g3);

        double pos = Volatile.Read(ref _position);
        var s0 = _stems[0];
        var s1 = _stems[1];
        var s2 = _stems[2];
        var s3 = _stems[3];

        int outFrames = buffer.Length / 2;       // stereo
        int maxSrcFrame = (_length / 2) - 2;     // need room for [n] and [n+1] interp

        // Unity-speed fast path: integer step, no interpolation. This is the
        // common case (tempo fader centred) — keep it as fast as the old code.
        if (speed == 1f)
        {
            int ipos = (int)pos & ~1;            // align to a frame boundary
            int framesAvailable = (_length - ipos) / 2;
            int frames = Math.Min(outFrames, framesAvailable);
            if (frames <= 0) { EndOfStreamReached?.Invoke(this, EventArgs.Empty); return 0; }

            int samples = frames * 2;
            if (g0 == 1f && g1 == 1f && g2 == 1f && g3 == 1f)
            {
                for (int i = 0; i < samples; i++)
                    buffer[i] = s0[ipos + i] + s1[ipos + i] + s2[ipos + i] + s3[ipos + i];
            }
            else
            {
                for (int i = 0; i < samples; i++)
                    buffer[i] = s0[ipos + i] * g0 + s1[ipos + i] * g1
                              + s2[ipos + i] * g2 + s3[ipos + i] * g3;
            }
            Volatile.Write(ref _position, ipos + samples);
            ApplyFadeIn(buffer[..samples]);
            return samples;
        }

        // Speed != 1: vinyl-style linear interpolation. Each output frame reads
        // from a fractional source-frame index; we advance the source position
        // by `speed` frames per output frame. Sub-frame precision (double pos)
        // avoids drift over long tracks.
        int writtenFrames = 0;
        double srcFrame = pos / 2.0;             // convert interleaved-sample pos → frame pos
        for (int of = 0; of < outFrames; of++)
        {
            int iFrame = (int)srcFrame;
            if (iFrame >= maxSrcFrame) break;
            float frac = (float)(srcFrame - iFrame);

            int idx = iFrame * 2;
            float l0a = s0[idx],   l0b = s0[idx + 2];
            float l1a = s1[idx],   l1b = s1[idx + 2];
            float l2a = s2[idx],   l2b = s2[idx + 2];
            float l3a = s3[idx],   l3b = s3[idx + 2];
            float r0a = s0[idx+1], r0b = s0[idx + 3];
            float r1a = s1[idx+1], r1b = s1[idx + 3];
            float r2a = s2[idx+1], r2b = s2[idx + 3];
            float r3a = s3[idx+1], r3b = s3[idx + 3];

            float left  = (l0a + frac * (l0b - l0a)) * g0
                        + (l1a + frac * (l1b - l1a)) * g1
                        + (l2a + frac * (l2b - l2a)) * g2
                        + (l3a + frac * (l3b - l3a)) * g3;
            float right = (r0a + frac * (r0b - r0a)) * g0
                        + (r1a + frac * (r1b - r1a)) * g1
                        + (r2a + frac * (r2b - r2a)) * g2
                        + (r3a + frac * (r3b - r3a)) * g3;

            buffer[of * 2] = left;
            buffer[of * 2 + 1] = right;
            srcFrame += speed;
            writtenFrames++;
        }

        Volatile.Write(ref _position, srcFrame * 2.0);
        if (writtenFrames == 0) EndOfStreamReached?.Invoke(this, EventArgs.Empty);
        int produced = writtenFrames * 2;
        ApplyFadeIn(buffer[..produced]);
        return produced;
    }

    public void Seek(int offset)
    {
        var clamped = Math.Clamp(offset, 0, _length);
        Volatile.Write(ref _position, (double)clamped);
        Volatile.Write(ref _fadeRemaining, FadeFrames);
        PositionChanged?.Invoke(this, new PositionChangedEventArgs(clamped));
    }

    /// <summary>Crossfade out of the last output sample into the new audio whenever
    /// a Seek has armed the fade counter — pure declick, no audible gap. Also keeps
    /// <c>_lastOut*</c> up to date with whatever the final sample of this buffer
    /// ended up being, so the next seek has a continuous starting point.</summary>
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
                float t = 1f - (float)fade / FadeFrames;   // 0 → 1 across the ramp
                samples[f * 2]     = lastL + (samples[f * 2]     - lastL) * t;
                samples[f * 2 + 1] = lastR + (samples[f * 2 + 1] - lastR) * t;
                fade--;
            }
            Volatile.Write(ref _fadeRemaining, fade);
        }

        // Remember where this buffer ended so the next post-seek crossfade has
        // somewhere continuous to start from.
        _lastOutL = samples[(frames - 1) * 2];
        _lastOutR = samples[(frames - 1) * 2 + 1];
    }

    public void Dispose() => IsDisposed = true;
}
