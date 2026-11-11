using Microsoft.Extensions.Options;
using System.Threading;
using Sholto.App.Analysis;
using Sholto.App.Analysis.Stems;
using SoundFlow.Enums;
using SoundFlow.Interfaces;
using SoundFlow.Metadata.Models;
using SoundFlow.Structs;

namespace Sholto.App.Audio;

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
public sealed class StemMixDataProvider : ISoundDataProvider, IVarispeedProvider
{
    public const int StemCount = 4;
    public const int Drums = 0, Vocals = 1, Bass = 2, Other = 3;

    // Not readonly: Dispose() nulls out the entries so the 4 × ~40 MB stem
    // arrays can be reclaimed by GC even if some other code briefly keeps a
    // reference to the provider itself (e.g. SoundPlayer hasn't finalised yet).
    // The audio thread won't read after Dispose because the SoundPlayer has
    // been Disposed first — see Deck.TearDownPlayers.
    private float[][]? _stems;
    private readonly int _length;            // total interleaved-sample length per stem (always even — stereo)
    private double _position;                // fractional source position in interleaved samples (sub-sample precision for vinyl-mode speed); written only by the audio thread, inside ReadBytes

    // Seek requests from other threads. Seek never touches _position: it parks
    // the clamped target here (Interlocked.Exchange) and ReadBytes takes it
    // once at the top of the next buffer, so a seek can't be overwritten by a
    // read that was already in flight. NoPendingSeek = nothing requested.
    // A later Seek before the next buffer simply replaces an earlier one.
    private const long NoPendingSeek = long.MinValue;
    private long _pendingSeek = NoPendingSeek;

    // Per-stem gain. Set via SetGain from UI/MIDI threads; read once per buffer
    // by the audio thread. Volatile keeps the read/write safe across cores
    // without locks.
    private float _g0 = 1f, _g1 = 1f, _g2 = 1f, _g3 = 1f;

    // Vinyl-style speed (1.0 = unity, <1 slower & lower pitch, >1 faster & higher,
    // 0 = held/frozen, negative = reverse). Negative speed is how a platter
    // scratch drives this provider (see Deck.ScratchRate) — the tempo fader
    // never goes negative, so sign alone tells ReadChunk which regime it's in.
    // Set lock-free; the audio thread reads it once per buffer.
    private float _speed = 1f;
    public void SetSpeed(float speed) => Volatile.Write(ref _speed, speed);

    // Active loop region (see StemLoopBounds), published by the control thread; null = no loop.
    private StemLoopBounds? _loopBounds;      // published by control thread (Volatile)
    public void SetLoop(LoopRegion? loop)
    {
        Volatile.Write(ref _loopBounds,
            loop is { } r ? new StemLoopBounds(r.StartSample, r.EndSample) : null);
    }
    public LoopRegion? ActiveLoop
    {
        get
        {
            var bounds = Volatile.Read(ref _loopBounds);
            return bounds is null ? null : new LoopRegion(bounds.Start, bounds.End);
        }
    }

    // Crossfade length at the loop wrap, from LoopOptions.FadeMs (default 256 frames, ~5.3 ms at
    // 48 kHz). Clamped to 24..4800 frames (0.5-100 ms) so the audio thread never mixes a fade
    // longer than 100 ms (would smear transients badly).
    private readonly int _loopFadeFrames;

    private StemLoopTail? _loopTail;          // published by UI thread (Volatile)
    private StemLoopCrossfade _loopCrossfade; // wrap crossfade state; only audio thread touches

    /// <summary>Build the crossfade tail for the current loop. Reads
    /// the loop fade length frames of source audio starting at
    /// <paramref name="loopEndSample"/> (per stem, no gain applied — gains
    /// are baked in at fade-mix time so mutes are respected). Call from the
    /// UI thread immediately BEFORE <see cref="SetLoop"/>, and again on any
    /// <c>loopEnd</c> change (halve/double). Pass <c>loopEndSample</c> in
    /// interleaved-sample units, same as the rest of this class.</summary>
    public void BuildLoopTail(long loopEndSample)
    {
        var stems = _stems;
        if (stems is null)
        {
            Volatile.Write(ref _loopTail, null);
            return;
        }
        long available = Math.Max(0, _length - loopEndSample) / 2;
        int frames = (int)Math.Min(_loopFadeFrames, available);
        if (frames <= 0)
        {
            Volatile.Write(ref _loopTail, null);
            return;
        }
        var tail = new float[StemCount][];
        for (int s = 0; s < StemCount; s++)
        {
            tail[s] = new float[frames * 2];
            Array.Copy(stems[s], loopEndSample, tail[s], 0, frames * 2);
        }
        Volatile.Write(ref _loopTail, new StemLoopTail(tail, frames));
    }

    // Seek declick and loop-wrap crossfade: audio-thread-only mutable structs, called in place.
    private StemFadeIn _fadeIn;

    public StemMixDataProvider(StemSamples stems, int sampleRate, IOptions<LoopOptions> loopOptions)
    {
        if (stems is null || stems.Drums is null || stems.Vocals is null || stems.Bass is null || stems.Other is null)
            throw new ArgumentNullException();

        _stems = new[] { stems.Drums, stems.Vocals, stems.Bass, stems.Other };
        _length = Math.Min(Math.Min(stems.Drums.Length, stems.Vocals.Length),
                           Math.Min(stems.Bass.Length, stems.Other.Length));
        SampleRate = sampleRate;
        // 48 frames per ms at 48 kHz.
        _loopFadeFrames = Math.Clamp((int)(loopOptions.Value.FadeMs * 48 + 1e-6), 24, 4800);
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

    // A seek not yet applied by the audio thread is reported as the position,
    // so a caller reading Position straight after Seek sees its own target.
    public int Position
    {
        get
        {
            long pending = Volatile.Read(ref _pendingSeek);
            return pending != NoPendingSeek ? (int)pending : (int)Volatile.Read(ref _position);
        }
    }
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
        // Apply any seek requested since the last buffer. Taken exactly once;
        // this is the only place a Seek reaches _position, and it runs on the
        // audio thread, so _position has a single writer.
        long pendingSeek = Interlocked.Exchange(ref _pendingSeek, NoPendingSeek);
        if (pendingSeek != NoPendingSeek)
        {
            Volatile.Write(ref _position, (double)pendingSeek);
            _fadeIn.Arm();
        }

        // Snapshot speed + gains + loop once per buffer.
        float speed = Volatile.Read(ref _speed);
        float g0 = Volatile.Read(ref _g0);
        float g1 = Volatile.Read(ref _g1);
        float g2 = Volatile.Read(ref _g2);
        float g3 = Volatile.Read(ref _g3);
        var loopBoundsSnap = Volatile.Read(ref _loopBounds);
        long loopStartSnap = loopBoundsSnap?.Start ?? long.MinValue;
        long loopEndSnap   = loopBoundsSnap?.End ?? long.MinValue;
        bool looping = loopBoundsSnap is not null && loopEndSnap > loopStartSnap;
        // Latest tail published by UI thread; used to ARM a new fade. The
        // audio thread, once a fade is in flight, holds the chosen tail in
        // the crossfade so a concurrent BuildLoopTail can't shift the
        // ramp mid-fade.
        var loopTailSnap = Volatile.Read(ref _loopTail);

        // Dispose may have nulled _stems out from under us between buffers.
        var stems = _stems;
        if (stems is null) return 0;

        int outFrames = buffer.Length / 2;
        int written = 0;

        // Chunked outer loop: each iteration fills up to the next "must stop"
        // boundary — either the loop-end (if looping) or end-of-track — then
        // wraps or breaks. A single buffer can survive any number of wraps,
        // which matters for very short loops.
        while (written < outFrames)
        {
            int sampleOffset = written * 2;
            int produced = ReadChunk(buffer.Slice(sampleOffset),
                outFrames - written, speed, g0, g1, g2, g3,
                stems, looping, loopStartSnap, loopEndSnap);
            if (produced == 0) break;

            // If a wrap-crossfade is in flight (armed by a previous wrap, in
            // this buffer or an earlier one), mix the tail into the START of
            // the frames we just produced. The fade ramps the tail OUT while
            // the fresh post-wrap audio (already in `buffer`) ramps IN, so the
            // first frame after a wrap is sample-continuous with what played
            // just before it.
            if (_loopCrossfade.IsActive)
            {
                _loopCrossfade.Mix(buffer.Slice(sampleOffset, produced * 2),
                    g0, g1, g2, g3);
            }

            written += produced;

            // Wrap. Subtract the period (don't snap to loopStart) so any
            // sub-sample carry from vinyl-mode speed is preserved across the
            // seam — the next frame is read at exactly the phase the
            // resampler was on, not rounded down to an integer position.
            double pos = Volatile.Read(ref _position);
            if (looping && pos >= loopEndSnap)
            {
                long period = loopEndSnap - loopStartSnap;
                Volatile.Write(ref _position, pos - period);

                // Arm a fade for the NEXT chunk (which will read fresh audio
                // from loopStart). Skip if a fade is already running — for a
                // loop short enough that we wrap twice within the loop fade length
                // (< 5 ms at default settings, which would mean a loop length
                // < 5 ms — vanishingly unlikely in DJ use) we'd otherwise
                // restart the ramp mid-fade and click.
                if (loopTailSnap is not null)
                    _loopCrossfade.ArmIfIdle(loopTailSnap);
            }
        }

        int producedSamples = written * 2;
        if (written == 0) EndOfStreamReached?.Invoke(this, EventArgs.Empty);
        // Seek-armed declick fade (jog wheel / manual seeks only — wrap doesn't
        // arm this any more). Applied at end of buffer because Seek happens
        // between buffers, so the fade always lands at the buffer start.
        _fadeIn.Apply(buffer[..producedSamples]);
        return producedSamples;
    }

    /// <summary>Fill up to <paramref name="maxFrames"/> output frames, stopping
    /// at end-of-track or — when looping — at loop-out. Returns frames written.
    /// Position is advanced; the caller decides whether to wrap or break based
    /// on where Position landed.</summary>
    private int ReadChunk(Span<float> outBuf, int maxFrames, float speed,
        float g0, float g1, float g2, float g3,
        float[][] stems, bool looping, long loopStart, long loopEnd)
    {
        var s0 = stems[0];
        var s1 = stems[1];
        var s2 = stems[2];
        var s3 = stems[3];

        double pos = Volatile.Read(ref _position);
        int maxSrcFrame = (_length / 2) - 2;     // need room for [n] and [n+1] interp

        if (speed == 1f)
        {
            int ipos = (int)pos & ~1;            // align to a frame boundary
            // Source end for this chunk: loop-out (if looping and we're inside
            // the loop) wins over end-of-track. Outside the loop region, ignore
            // loop bounds — the user might have seeked away and we shouldn't
            // teleport them back.
            int chunkEnd = _length;
            if (looping && ipos < loopEnd && ipos >= loopStart)
                chunkEnd = (int)Math.Min(loopEnd, _length);
            int framesAvailable = (chunkEnd - ipos) / 2;
            int frames = Math.Min(maxFrames, framesAvailable);
            if (frames <= 0) return 0;

            int samples = frames * 2;
            if (g0 == 1f && g1 == 1f && g2 == 1f && g3 == 1f)
            {
                for (int i = 0; i < samples; i++)
                    outBuf[i] = s0[ipos + i] + s1[ipos + i] + s2[ipos + i] + s3[ipos + i];
            }
            else
            {
                for (int i = 0; i < samples; i++)
                    outBuf[i] = s0[ipos + i] * g0 + s1[ipos + i] * g1
                              + s2[ipos + i] * g2 + s3[ipos + i] * g3;
            }
            Volatile.Write(ref _position, (double)(ipos + samples));
            return frames;
        }

        // Vinyl-speed path: linear-interp resample. Stop the chunk before the
        // source crosses loop-out (or track-end), so the caller's wrap logic
        // runs at the right moment.
        double srcFrame = pos / 2.0;

        if (speed < 0f)
        {
            // Reverse (platter scratch backspin — the tempo fader never goes
            // negative, so this branch is scratch-only). Loop bounds don't
            // apply: a loop can't be active while a top-platter scratch is in
            // progress (Orchestrator's jog handler ignores the wheel on a
            // loop-locked deck), so the only boundary here is the start of
            // the track.
            int of = 0;
            for (; of < maxFrames; of++)
            {
                if (srcFrame <= 0.0) break;
                int iFrame = (int)Math.Floor(srcFrame);
                if (iFrame >= maxSrcFrame) iFrame = maxSrcFrame - 1; // guard a stray overshoot
                float frac = (float)(srcFrame - iFrame);
                WriteInterpolatedFrame(outBuf, of, iFrame, frac, s0, s1, s2, s3, g0, g1, g2, g3);
                srcFrame += speed;
            }
            if (of < maxFrames)
            {
                // Ran off the start of the track mid-chunk. A real record just
                // runs out of groove and goes quiet — silence the remainder and
                // pin position at 0, rather than returning a short/zero chunk
                // (SoundFlow's SoundPlayer treats that as end-of-stream and
                // stops the deck, which is wrong mid-scratch: the user is
                // still holding the platter).
                outBuf.Slice(of * 2, (maxFrames - of) * 2).Clear();
                srcFrame = 0.0;
                of = maxFrames;
            }
            Volatile.Write(ref _position, srcFrame * 2.0);
            return of;
        }

        // Forward (speed >= 0): tempo-fader playback, or a forward scratch
        // fling. speed == 0 is a parked platter — the loop below just keeps
        // re-emitting the same frame (frac stays 0), which is the correct
        // "needle motionless" sound and, crucially, still returns a full
        // chunk so the caller never sees a false end-of-stream.
        double boundaryFrame = looping && srcFrame * 2.0 >= loopStart && srcFrame * 2.0 < loopEnd
            ? loopEnd / 2.0
            : maxSrcFrame;
        int writtenFrames = 0;
        for (int of2 = 0; of2 < maxFrames; of2++)
        {
            if (srcFrame >= boundaryFrame) break;
            int iFrame = (int)Math.Floor(srcFrame);
            if (iFrame >= maxSrcFrame) break;
            float frac = (float)(srcFrame - iFrame);
            WriteInterpolatedFrame(outBuf, of2, iFrame, frac, s0, s1, s2, s3, g0, g1, g2, g3);
            srcFrame += speed;
            writtenFrames++;
        }
        Volatile.Write(ref _position, srcFrame * 2.0);
        return writtenFrames;
    }

    /// <summary>Linear-interpolate one output frame (both channels, all 4
    /// stems summed) from source frame <paramref name="iFrame"/> /
    /// <paramref name="iFrame"/>+1 at fractional position
    /// <paramref name="frac"/>, and write it to <paramref name="outBuf"/> at
    /// <paramref name="frameIndex"/>. Shared by the forward and reverse
    /// vinyl-speed paths — the interpolation math doesn't care which
    /// direction <c>srcFrame</c> is moving, only where it currently sits.</summary>
    private void WriteInterpolatedFrame(Span<float> outBuf, int frameIndex, int iFrame, float frac,
        float[] s0, float[] s1, float[] s2, float[] s3,
        float g0, float g1, float g2, float g3)
    {
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

        int o = frameIndex * 2;
        outBuf[o]     = left;
        outBuf[o + 1] = right;
    }

    public void Seek(int offset)
    {
        var clamped = Math.Clamp(offset, 0, _length);
        Interlocked.Exchange(ref _pendingSeek, clamped);
        PositionChanged?.Invoke(this, new PositionChangedEventArgs(clamped));
    }

    public void Dispose()
    {
        IsDisposed = true;
        // Release the 4 × stem buffers. ReadBytes guards against this so the
        // audio thread won't NRE if it's still running when Dispose fires.
        // Volatile to publish the null reliably across threads.
        Volatile.Write(ref _stems, null);
        Volatile.Write(ref _loopTail, null);
    }
}
