namespace Sholto.Analysis;

/// <summary>
/// Turns madmom's raw, sometimes-irregular downbeat detections into the
/// constant-spacing "beatgrid" every DJ tool actually shows on screen.
///
/// Why this exists: madmom's DBN tracks tempo as a Bayesian latent state, so
/// raw downbeats can wobble (intro at wrong perceived tempo, half-time → full
/// mix lock, time-varying tempo in live recordings). DJs need a constant grid
/// derived from a single BPM + a single trusted phase anchor so beat-jumping,
/// quantised loops, and visual alignment all behave predictably. This is what
/// Rekordbox / Serato / Traktor do too — they don't draw raw beat detections.
/// </summary>
public static class Beatgrid
{
    /// <summary>Synthesize a constant-spacing downbeat grid covering the track.
    /// Returns an empty array if BPM or duration are missing — caller should
    /// fall back to "no grid" rather than guessing. Equivalent to
    /// <see cref="SynthesizeFullGrid"/>.Downbeats; kept for callers that only
    /// need the downbeats.</summary>
    public static double[] Synthesize(
        double bpm,
        double[] rawBeats,
        double[] rawDownbeats,
        double durationSec)
        => SynthesizeFullGrid(bpm, rawBeats, rawDownbeats, durationSec).Downbeats;

    /// <summary>
    /// Synthesize both the per-beat grid and the per-bar (downbeat) grid from
    /// the same constant-spacing math. Crucially, beats and downbeats share an
    /// anchor + period so every Nth beat is a downbeat by construction. This
    /// is what guarantees the waveform's small per-beat ticks line up exactly
    /// with the tall downbeat bars — without it, the two would drift apart by
    /// 1-2 columns whenever the synth anchor didn't fall on a raw beat.
    /// </summary>
    public static (double[] Beats, double[] Downbeats) SynthesizeFullGrid(
        double bpm,
        double[] rawBeats,
        double[] rawDownbeats,
        double durationSec)
    {
        if (bpm <= 0 || durationSec <= 0) return ([], []);

        int beatsPerBar = InferBeatsPerBar(rawBeats, rawDownbeats);
        double beatPeriod = 60.0 / bpm;
        double barPeriod  = beatPeriod * beatsPerBar;
        if (barPeriod <= 0) return ([], []);

        double anchor = ComputeAnchor(rawDownbeats, barPeriod);

        // Anchor is in [0, barPeriod). Walk backward to the first downbeat
        // ≥ 0 so we cover the very start of the song.
        double t0 = anchor;
        while (t0 - barPeriod >= 0) t0 -= barPeriod;

        var downbeats = new List<double>(capacity: (int)(durationSec / barPeriod) + 2);
        var beats     = new List<double>(capacity: (int)(durationSec / beatPeriod) + 2);

        for (double db = t0; db <= durationSec + barPeriod / 2; db += barPeriod)
        {
            if (db >= 0) downbeats.Add(db);
            // Emit beatsPerBar beats starting AT this downbeat. The first one
            // IS the downbeat itself; the next (beatsPerBar - 1) are the
            // intermediate beats.
            for (int i = 0; i < beatsPerBar; i++)
            {
                double bt = db + i * beatPeriod;
                if (bt >= 0 && bt <= durationSec + beatPeriod / 2) beats.Add(bt);
            }
        }
        return (beats.ToArray(), downbeats.ToArray());
    }

    /// <summary>Beats-per-bar from the *mode* of beat-counts between consecutive
    /// raw downbeats. 4 is overwhelmingly correct for DJ-able music; we only
    /// pick 3 if the evidence is strong (mostly-3 distribution).</summary>
    private static int InferBeatsPerBar(double[] beats, double[] downbeats)
    {
        if (downbeats.Length < 2 || beats.Length < 2) return 4;

        int threes = 0, fours = 0, other = 0;
        for (int i = 1; i < downbeats.Length; i++)
        {
            double span = downbeats[i] - downbeats[i - 1];
            if (span <= 0) continue;
            // Count beats strictly inside (downbeats[i-1], downbeats[i]].
            int n = 0;
            foreach (var b in beats)
            {
                if (b > downbeats[i - 1] + 1e-6 && b <= downbeats[i] + 1e-6) n++;
            }
            if (n == 3) threes++;
            else if (n == 4) fours++;
            else other++;
        }
        // Need a clear majority of 3s to pick 3 — guards against a single weird
        // intro bar making us misgrid the whole song.
        if (threes > fours && threes >= (threes + fours + other) * 0.6) return 3;
        return 4;
    }

    /// <summary>Find the phase in [0, period) that best fits the raw downbeats.
    /// Robust to outliers: projects each downbeat to its phase, finds the
    /// densest half-period window on the circle, averages the phases inside.</summary>
    private static double ComputeAnchor(double[] downbeats, double period)
    {
        if (downbeats.Length == 0) return 0;

        // Phase = downbeat mod period, normalized to [0, period).
        var phases = new double[downbeats.Length];
        for (int i = 0; i < phases.Length; i++)
        {
            double p = downbeats[i] % period;
            phases[i] = p < 0 ? p + period : p;
        }

        if (phases.Length == 1) return phases[0];

        Array.Sort(phases);
        double halfPeriod = period / 2;

        // For each candidate starting phase, count phases inside a half-period
        // window. Best window = densest cluster of "correct" downbeats; outliers
        // outside the window get ignored.
        int bestStart = 0, bestCount = 0;
        for (int i = 0; i < phases.Length; i++)
        {
            int count = 0;
            double start = phases[i];
            for (int j = 0; j < phases.Length; j++)
            {
                double d = phases[j] - start;
                if (d < 0) d += period;
                if (d < halfPeriod) count++;
            }
            if (count > bestCount) { bestCount = count; bestStart = i; }
        }

        // Mean of phases inside the best window. Unroll wrap-around by adding
        // `period` to phases that landed below the window's start.
        double startVal = phases[bestStart];
        double sum = 0;
        int n2 = 0;
        for (int j = 0; j < phases.Length; j++)
        {
            double d = phases[j] - startVal;
            if (d < 0) d += period;
            if (d < halfPeriod) { sum += startVal + d; n2++; }
        }
        double anchor = sum / n2;
        anchor %= period;
        if (anchor < 0) anchor += period;
        return anchor;
    }
}
