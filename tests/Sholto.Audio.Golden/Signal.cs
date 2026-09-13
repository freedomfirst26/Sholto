namespace Sholto.Audio.Golden;

/// <summary>
/// Deterministic synthetic test signals — no dependency on any file on disk
/// (never mind the user's music), so the golden tests are fully reproducible
/// from source. Two different three-band tone stacks (deck A / deck B) give
/// gain/crossfade tests something to tell apart, and enough low/mid/high
/// energy for the EQ and filter tests to have something to cut.
/// </summary>
internal static class Signal
{
    public const int SampleRate = 48000;

    /// <summary>Deck A: 110 Hz (low) + 1200 Hz (mid) + 7500 Hz (high) + a touch
    /// of deterministic broadband noise (LCG, fixed seed — never
    /// <see cref="Random"/> without a seed), stereo with a small L/R phase
    /// offset so the two channels aren't identical.</summary>
    public static float[] ToneStackA(double seconds) => ToneStack(seconds, low: 110, mid: 1200, high: 7500, seed: 1);

    /// <summary>Deck B: a different stack (160/1800/6000 Hz) so a crossfade or
    /// gain test can tell which deck's energy is present in the mix.</summary>
    public static float[] ToneStackB(double seconds) => ToneStack(seconds, low: 160, mid: 1800, high: 6000, seed: 2);

    private static float[] ToneStack(double seconds, double low, double mid, double high, int seed)
    {
        int frames = (int)Math.Round(seconds * SampleRate);
        var samples = new float[frames * 2];
        uint lcg = (uint)(seed * 2654435761u + 1);
        for (int i = 0; i < frames; i++)
        {
            double t = i / (double)SampleRate;
            double core = 0.22 * Math.Sin(2 * Math.PI * low * t)
                        + 0.18 * Math.Sin(2 * Math.PI * mid * t)
                        + 0.10 * Math.Sin(2 * Math.PI * high * t);

            // Simple deterministic LCG noise, ~1% amplitude, for broadband
            // content (helps the filter sweep show a visible, not just
            // tonal, effect). Same generator, same seed, every run.
            lcg = lcg * 1664525u + 1013904223u;
            double noise = ((lcg >> 8) / (double)(1u << 24) - 0.5) * 0.02;

            double left = core + noise;
            // Right channel: same content, small phase offset on the mid
            // partial only, so L/R aren't bit-identical (stereo, not dual-mono)
            // without changing overall band energy.
            double right = 0.22 * Math.Sin(2 * Math.PI * low * t)
                         + 0.18 * Math.Sin(2 * Math.PI * mid * t + 0.35)
                         + 0.10 * Math.Sin(2 * Math.PI * high * t)
                         + noise;

            samples[i * 2] = (float)left;
            samples[i * 2 + 1] = (float)right;
        }
        return samples;
    }
}
