namespace Sholto.Audio;

/// <summary>
/// Stateless tempo-derived arithmetic shared by effects that need a beat
/// count converted to a sample count (e.g. <see cref="EchoEffect"/> and
/// <see cref="BeatRepeatEffect"/>). Control-thread only: called from a
/// <c>SetTempo</c> / <c>Beats</c>-setter pair, never from <c>Process</c> or
/// <c>ProcessSample</c>. Pure function — callers own publication (e.g.
/// <c>Volatile.Write</c>) of the result themselves.
/// </summary>
public static class TempoMath
{
    /// <summary>Converts a beat count at a given tempo to a sample count,
    /// clamped to fit a ring/capture buffer of <paramref name="capacityFrames"/>.
    /// Verbatim extraction of the formula both effects used to duplicate:
    /// <c>beats * (60/bpm) * sampleRate</c>, rounded, then clamped to
    /// <c>[1, capacityFrames - 1]</c>.</summary>
    public static int BeatsToSamples(double beats, double bpm, int sampleRate, int capacityFrames)
    {
        int samples = (int)Math.Round(beats * (60.0 / bpm) * sampleRate);
        return Math.Clamp(samples, 1, capacityFrames - 1);
    }

    /// <summary>One-pole ramp coefficient for a ~5 ms gain smoothing, shared
    /// by both effects' identical <c>_gainAlpha</c> computation:
    /// <c>1 - exp(-1 / (0.005 * sampleRate))</c>.</summary>
    public static float FiveMsGainAlpha(int sampleRate) => 1f - MathF.Exp(-1f / (0.005f * sampleRate));
}
