namespace Sholto.App.Audio;

/// <summary>
/// The loop-wrap crossfade state of <see cref="StemMixDataProvider"/>. Touched
/// only by the audio thread. A mutable struct: it must live in a non-readonly
/// field and be called in place, never copied, or its state changes are lost.
/// </summary>
internal struct StemLoopCrossfade
{
    private StemLoopTail? _activeTail;    // captured at wrap-time by audio thread
    private int _remaining;               // frames left to crossfade

    public readonly bool IsActive => _remaining > 0 && _activeTail is not null;

    /// <summary>Arm a fade from <paramref name="tail"/> unless one is already
    /// running — for a loop short enough that we wrap twice within the loop fade
    /// length we'd otherwise restart the ramp mid-fade and click.</summary>
    public void ArmIfIdle(StemLoopTail tail)
    {
        if (_remaining == 0)
        {
            _activeTail = tail;
            _remaining = tail.Frames;
        }
    }

    /// <summary>Crossfade the loop-tail samples into the start of
    /// <paramref name="freshChunk"/>. Called immediately after a chunk-read
    /// when the fade is active — the listener has just
    /// crossed the loop seam and the first <c>_remaining</c> frames
    /// of fresh audio need to be blended with the natural continuation past
    /// loopEnd (captured in <c>_activeTail</c>). Decrements
    /// <c>_remaining</c> by the frames mixed and releases the
    /// tail reference when the fade completes so the GC can reclaim it.
    /// Stem gains are applied to the tail at mix time so a stem muted
    /// during playback stays muted across the seam.</summary>
    public void Mix(Span<float> freshChunk, float g0, float g1, float g2, float g3)
    {
        var tail = _activeTail!;
        int totalFade = tail.Frames;
        int chunkFrames = freshChunk.Length / 2;
        int framesToMix = Math.Min(_remaining, chunkFrames);

        // `tailStart` is where in the tail buffer this chunk continues from
        // — for a fade that straddles a buffer boundary, we resume mid-tail.
        int tailFramesConsumed = totalFade - _remaining;
        int tailStart = tailFramesConsumed * 2;

        var t0 = tail.Stems[0];
        var t1 = tail.Stems[1];
        var t2 = tail.Stems[2];
        var t3 = tail.Stems[3];

        float invTotal = 1f / totalFade;
        for (int f = 0; f < framesToMix; f++)
        {
            int outIdx = f * 2;
            int tIdx = tailStart + f * 2;
            float tL = t0[tIdx]     * g0 + t1[tIdx]     * g1 + t2[tIdx]     * g2 + t3[tIdx]     * g3;
            float tR = t0[tIdx + 1] * g0 + t1[tIdx + 1] * g1 + t2[tIdx + 1] * g2 + t3[tIdx + 1] * g3;
            // ramp = (frames already mixed) / total — goes 0 → 1 across the fade
            float ramp = (tailFramesConsumed + f) * invTotal;
            float oneMinus = 1f - ramp;
            freshChunk[outIdx]     = tL * oneMinus + freshChunk[outIdx]     * ramp;
            freshChunk[outIdx + 1] = tR * oneMinus + freshChunk[outIdx + 1] * ramp;
        }
        _remaining -= framesToMix;
        if (_remaining == 0) _activeTail = null;
    }
}
