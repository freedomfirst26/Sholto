using System.Threading;

namespace Sholto.App.Audio;

/// <summary>
/// The seek declick of <see cref="StemMixDataProvider"/>. Naïve "fade in from 0"
/// produces its OWN click at the start of the fade because the previous buffer
/// ended at some non-zero value. So we remember the last output sample per
/// channel and crossfade from it to the new audio over a few ms — no step at
/// either end. ~5 ms at 48 kHz is short enough to be inaudible as a gap but long
/// enough to smooth the discontinuity.
/// Touched only by the audio thread. A mutable struct: it must live in a
/// non-readonly field and be called in place, never copied, or its state
/// changes are lost.
/// </summary>
internal struct StemFadeIn
{
    private const int FadeFrames = 256;
    private int _remaining;
    private float _lastOutL, _lastOutR;

    /// <summary>Start a fade over the next <c>FadeFrames</c> frames.</summary>
    public void Arm() => Volatile.Write(ref _remaining, FadeFrames);

    /// <summary>Crossfade out of the last output sample into the new audio whenever
    /// a Seek has armed the fade counter — pure declick, no audible gap. Also keeps
    /// the last output sample up to date with whatever the final sample of this buffer
    /// ended up being, so the next seek has a continuous starting point.</summary>
    public void Apply(Span<float> samples)
    {
        int frames = samples.Length / 2;
        if (frames == 0) return;

        int fade = Volatile.Read(ref _remaining);
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
            Volatile.Write(ref _remaining, fade);
        }

        // Remember where this buffer ended so the next post-seek crossfade has
        // somewhere continuous to start from.
        _lastOutL = samples[(frames - 1) * 2];
        _lastOutR = samples[(frames - 1) * 2 + 1];
    }
}
