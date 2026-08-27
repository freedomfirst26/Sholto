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

    /// <summary>Regenerate a constant-spacing grid at a given BPM, pivoting
    /// around <paramref name="anchorSec"/> so that one downbeat stays fixed
    /// at that time while the spacing stretches/shrinks around it. Used by
    /// the live BPM-width adjustment: the user keeps the kick they're looking
    /// at pinned and only the bar SPACING changes, so distant kicks stop
    /// drifting. beatsPerBar is passed in (caller derives it from the
    /// existing grid) rather than re-inferred, so a width tweak can't
    /// accidentally flip 4/4 ↔ 3/4.</summary>
    public static (double[] Beats, double[] Downbeats) SynthesizeAnchored(
        double bpm, double anchorSec, int beatsPerBar, double durationSec)
    {
        if (bpm <= 0 || durationSec <= 0 || beatsPerBar < 1) return ([], []);

        double beatPeriod = 60.0 / bpm;
        double barPeriod  = beatPeriod * beatsPerBar;

        // Walk the anchor back to the first downbeat ≥ 0 so we cover the start.
        double t0 = anchorSec;
        while (t0 - barPeriod >= 0) t0 -= barPeriod;
        while (t0 < 0) t0 += barPeriod;

        var downbeats = new List<double>(capacity: (int)(durationSec / barPeriod) + 2);
        var beats     = new List<double>(capacity: (int)(durationSec / beatPeriod) + 2);

        for (double db = t0; db <= durationSec + barPeriod / 2; db += barPeriod)
        {
            if (db >= 0) downbeats.Add(db);
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
    /// <summary>Number of opening downbeats used to anchor the grid phase.</summary>
    private const int AnchorWindowBars = 16;

    /// <summary>Result of <see cref="FitGrid"/>. When <see cref="UsedFit"/> is
    /// false, the caller should ignore Bpm/AnchorSec/ResidualRmsMs and fall
    /// back to (reported BPM, first detected downbeat) as before.</summary>
    public readonly record struct GridFitResult(
        bool UsedFit, double Bpm, double AnchorSec, double ResidualRmsMs, string Reason);

    /// <summary>Minimum beat count required to trust a least-squares fit over
    /// the reported BPM.</summary>
    private const int MinFitBeats = 8;

    /// <summary>Fitted period may not differ from the reported 60/Bpm period
    /// by more than this fraction, else the fit is treated as bogus (wrong
    /// beat/half-beat lock, octave error, etc).</summary>
    private const double MaxPeriodRelError = 0.05;

    /// <summary>Residual RMS above this means the track isn't constant-tempo
    /// (or beats are too noisy) — a rigid grid would be wrong, so fall back.</summary>
    private const double MaxResidualRmsSec = 0.025;

    /// <summary>Least-squares fit a constant-spacing grid t(n) = anchor + n*period
    /// through every raw beat detection, rather than trusting just the reported
    /// BPM + first downbeat. A single reported BPM that's off by a few hundredths
    /// (175.0 vs a true 175.04) makes a rigid synthesized grid drift visibly off
    /// the kicks by the end of a track; regressing through all of madmom's beats
    /// finds the period that actually matches what was detected. The fitted
    /// anchor's phase is then snapped to the nearest fitted-grid line to the
    /// first detected downbeat, so bar 1 still lands on a real downbeat.
    /// Falls back (UsedFit = false) when there isn't enough data, the fit
    /// disagrees too much with the reported BPM, or beats are too irregular
    /// for a single constant tempo to make sense (variable-tempo track).</summary>
    public static GridFitResult FitGrid(double[] beatTimes, double reportedBpm, double firstDownbeatSec)
    {
        int n = beatTimes.Length;
        if (n < MinFitBeats)
            return new GridFitResult(false, 0, 0, 0, $"too few beats ({n} < {MinFitBeats})");

        // Simple linear regression of t[i] against index i.
        double sumI = 0, sumT = 0, sumIT = 0, sumII = 0;
        for (int i = 0; i < n; i++)
        {
            sumI += i;
            sumT += beatTimes[i];
            sumIT += i * beatTimes[i];
            sumII += (double)i * i;
        }
        double denom = n * sumII - sumI * sumI;
        if (denom <= 0)
            return new GridFitResult(false, 0, 0, 0, "degenerate regression");

        double period = (n * sumIT - sumI * sumT) / denom;
        double anchor0 = (sumT - period * sumI) / n;
        if (period <= 0)
            return new GridFitResult(false, 0, 0, 0, $"non-positive fitted period ({period:F4}s)");

        double sqErr = 0;
        for (int i = 0; i < n; i++)
        {
            double resid = beatTimes[i] - (anchor0 + i * period);
            sqErr += resid * resid;
        }
        double residualRmsSec = Math.Sqrt(sqErr / n);

        double fittedBpm = 60.0 / period;

        if (reportedBpm > 0)
        {
            double reportedPeriod = 60.0 / reportedBpm;
            double relError = Math.Abs(period - reportedPeriod) / reportedPeriod;
            if (relError > MaxPeriodRelError)
                return new GridFitResult(false, fittedBpm, anchor0, residualRmsSec * 1000,
                    $"fitted BPM {fittedBpm:F2} disagrees with reported {reportedBpm:F2} by {relError:P1}");
        }

        if (residualRmsSec > MaxResidualRmsSec)
            return new GridFitResult(false, fittedBpm, anchor0, residualRmsSec * 1000,
                $"residual RMS {residualRmsSec * 1000:F1}ms too irregular (variable tempo?)");

        // Snap the fitted anchor's phase so bar 1 lands on the real first
        // detected downbeat: shift by the whole number of beats that brings
        // anchor0 nearest firstDownbeatSec, without touching the fitted period.
        double k = Math.Round((firstDownbeatSec - anchor0) / period);
        double anchor = anchor0 + k * period;

        return new GridFitResult(true, fittedBpm, anchor, residualRmsSec * 1000,
            $"fit ok, {fittedBpm:F2} BPM (reported {reportedBpm:F2}), RMS {residualRmsSec * 1000:F1}ms");
    }

    private static double ComputeAnchor(double[] downbeats, double period)
    {
        if (downbeats.Length == 0) return 0;

        // Anchor the grid's phase to the OPENING downbeats only. madmom's beat
        // positions random-walk across a track — its DBN tracks tempo as a latent
        // state that wanders, so absolute beat times drift ±hundreds of ms over a
        // few minutes even when the true tempo is constant. Averaging the phase
        // over ALL downbeats therefore smears it and lands the grid up to ~a beat
        // off even at the very start. The opening bars are the freshest and are
        // where the DJ cues, so we estimate phase from them and leave any later
        // drift to the nudge / set-downbeat controls.
        int k = Math.Min(downbeats.Length, AnchorWindowBars);
        var phases = new double[k];
        for (int i = 0; i < k; i++)
        {
            double p = downbeats[i] % period;
            phases[i] = p < 0 ? p + period : p;
        }

        // Median of the opening phases: robust to a single mis-detected first
        // downbeat. Opening bars of a steady track share almost the same phase, so
        // there is no wrap-around seam to handle here.
        Array.Sort(phases);
        double anchor = phases[k / 2];
        anchor %= period;
        if (anchor < 0) anchor += period;
        return anchor;
    }
}
