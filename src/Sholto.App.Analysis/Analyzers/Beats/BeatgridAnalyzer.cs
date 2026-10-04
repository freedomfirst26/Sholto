namespace Sholto.App.Analysis.Analyzers.Beats;

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
///
/// Default <see cref="IBeatgridAnalyzer"/>. Pure computation — no environment/
/// filesystem/clock dependency — instance so a test/Bench harness can
/// substitute a fake; it holds no state of its own.
/// </summary>
public sealed class BeatgridAnalyzer(IBeatgridFactory beatgrids) : IBeatgridAnalyzer
{
    private readonly IBeatgridFactory _beatgrids = beatgrids;

    /// <summary>
    /// Fit a constant-spacing <see cref="Beatgrid"/> to madmom's raw beat and
    /// downbeat detections: tempo from the reported BPM, time signature from a
    /// majority vote over the raw bars, phase from the opening downbeats.
    ///
    /// Returns the four numbers, not arrays. Beats and downbeats share one
    /// anchor and one period, so every Nth beat is a downbeat by construction —
    /// which is what guarantees the waveform's small per-beat ticks line up
    /// exactly with the tall downbeat bars. Rendering them is
    /// <see cref="Beatgrid.Materialise"/>'s job, once.
    /// </summary>
    public Beatgrid FromDetections(
        double bpm,
        double[] rawBeats,
        double[] rawDownbeats,
        double durationSec)
    {
        if (bpm <= 0 || durationSec <= 0) return _beatgrids.None();

        int beatsPerBar = InferBeatsPerBar(rawBeats, rawDownbeats);
        double beatPeriod = 60.0 / bpm;
        double barPeriod  = beatPeriod * beatsPerBar;
        if (barPeriod <= 0) return _beatgrids.None();

        // ComputeAnchor returns a phase in [0, barPeriod), which is already the
        // first downbeat ≥ 0 — see Beatgrid.FirstDownbeatSec for the general case.
        double anchor = ComputeAnchor(rawDownbeats, barPeriod);
        return new Beatgrid(anchor, beatPeriod, beatsPerBar, durationSec);
    }

    /// <summary>Beats-per-bar from the *mode* of beat-counts between consecutive
    /// raw downbeats. 4 is overwhelmingly correct for DJ-able music; we only
    /// pick 3 if the evidence is strong (mostly-3 distribution).
    ///
    /// Deliberately NOT the same code as the array-reading <see cref="Beatgrid"/> constructor:
    /// this votes across madmom's noisy RAW bars, which genuinely disagree with
    /// each other; that one reads a single delta ratio off an already-synthesised
    /// constant-spacing grid, where one sample is exact.</summary>
    private int InferBeatsPerBar(double[] beats, double[] downbeats)
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

    private double ComputeAnchor(double[] downbeats, double period)
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
