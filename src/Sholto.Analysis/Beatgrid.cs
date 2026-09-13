using Sholto.Analysis.Analyzers;

namespace Sholto.Analysis;

/// <summary>
/// A constant-spacing beatgrid, and the whole of one: after fitting, every grid
/// in Sholto is described completely by these four numbers. Beats are at
/// <c>anchor + n·beatPeriod</c>, downbeats at every <see cref="BeatsPerBar"/>th
/// of them, out to <see cref="DurationSec"/>. There is no such thing as an
/// irregular grid here — madmom's raw, wobbling detections are
/// <see cref="DetectedBeats"/>, and <see cref="BeatgridFitter"/> is the one
/// place that turns those into one of these.
///
/// Why a value type and not two <c>double[]</c>: the arrays are a RENDERING of
/// the grid, not the grid. Consumers that hold only the arrays end up
/// reverse-engineering these four numbers back out of them — recovering
/// beats-per-bar from array deltas, binary-searching for a beat index that is
/// one <c>Math.Round</c>, indexing <c>beats[i + bars*4]</c> and needing a
/// fallback branch purely because the array happens to end. All of that is
/// arithmetic dressed up as lookup. Carry the grid; materialise only at the
/// edges that genuinely need an array.
///
/// Two such edges remain, both deliberate:
/// <list type="bullet">
/// <item><see cref="BasicAnalysis"/> stores the materialised arrays because the
/// on-disk analysis cache (<c>AnalysisCodec.Version</c> 3) stores them, and
/// changing that shape would invalidate every cached analysis in the user's
/// library and re-run madmom on every track.</item>
/// <item><c>WaveformControl</c> needs <c>double[]</c> for its
/// <c>AffectsRender</c> property and blit path.</item>
/// </list>
///
/// Immutable: the adjustment operations (<see cref="ShiftedBy"/>,
/// <see cref="AtBpm"/>, <see cref="PivotedAt"/>) each return a new grid, which
/// is what makes "detected grid" and "effective grid" two values rather than
/// one mutable object plus a pile of correction fields.
/// </summary>
/// <param name="AnchorSec">A time at which a DOWNBEAT falls. Not required to be
/// the first one, or even to be inside the track — <see cref="ShiftedBy"/> and
/// <see cref="PivotedAt"/> can move it anywhere. Materialisation normalises it
/// into the first bar (<see cref="FirstDownbeatSec"/>).</param>
/// <param name="BeatPeriodSec">Seconds per beat, i.e. <c>60 / bpm</c>. The
/// period, not the BPM, is the stored form: it is what every piece of grid
/// arithmetic actually uses, and storing BPM would round-trip a division
/// through every operation.</param>
/// <param name="BeatsPerBar">4 for almost all DJ-able music, 3 for waltz-time.
/// CARRIED, never re-derived: once it has been established it travels with the
/// grid, so a BPM tweak or a nudge cannot accidentally flip 4/4 to 3/4.</param>
/// <param name="DurationSec">Track length — the extent the materialisers emit
/// out to. Part of the grid because "the grid for this track" is bounded; two
/// grids with the same tempo and phase but different durations render
/// differently.</param>
public sealed record Beatgrid(
    double AnchorSec,
    double BeatPeriodSec,
    int BeatsPerBar,
    double DurationSec)
{
    /// <summary>The canonical "no grid" value: what a fitter returns when it was
    /// given a non-positive BPM or duration. Materialises to empty arrays, so
    /// callers that only ever render do not need a null check.</summary>
    public static Beatgrid Empty { get; } = new(0.0, 0.0, 4, 0.0);

    /// <summary>True when this grid describes nothing renderable. Mirrors
    /// exactly the guards the old tuple-returning synthesis methods used before
    /// returning <c>([], [])</c>.</summary>
    public bool IsEmpty => BeatPeriodSec <= 0 || DurationSec <= 0 || BeatsPerBar < 1;

    /// <summary>Tempo in beats per minute. Derived — <see cref="BeatPeriodSec"/>
    /// is the stored form.</summary>
    public double Bpm => BeatPeriodSec > 0 ? 60.0 / BeatPeriodSec : 0.0;

    /// <summary>Seconds per bar.</summary>
    public double BarPeriodSec => BeatPeriodSec * BeatsPerBar;

    /// <summary>The first downbeat at or after 0 — <see cref="AnchorSec"/>
    /// walked into <c>[0, BarPeriodSec)</c>. This is the origin of the index
    /// space used by <see cref="BeatAt"/>, <see cref="DownbeatAt"/> and the
    /// materialisers, so <c>BeatAt(i)</c> corresponds to <c>BeatTimes()[i]</c>.
    ///
    /// Deliberately repeated addition/subtraction rather than <c>%</c>: the
    /// synthesis this replaces walked the anchor in exactly this way, and the
    /// two do not agree bit-for-bit on every input.</summary>
    public double FirstDownbeatSec
    {
        get
        {
            double bar = BarPeriodSec;
            if (bar <= 0) return 0.0;
            double t0 = AnchorSec;
            while (t0 - bar >= 0) t0 -= bar;
            while (t0 < 0) t0 += bar;
            return t0;
        }
    }

    /// <summary>Time of beat <paramref name="index"/>, counting from
    /// <see cref="FirstDownbeatSec"/>. O(1), and defined for indices outside the
    /// track — the grid is infinite, only its materialisation is bounded.</summary>
    public double BeatAt(int index) => FirstDownbeatSec + index * BeatPeriodSec;

    /// <summary>Time of downbeat (bar start) <paramref name="index"/>. O(1).</summary>
    public double DownbeatAt(int index) => FirstDownbeatSec + index * BarPeriodSec;

    /// <summary>Index of the beat nearest <paramref name="timeSec"/>. One
    /// rounded division — this is what the binary searches over the beat array
    /// were computing the long way round. May return a negative index or one
    /// past the end of the track; callers that need a beat inside the track
    /// should clamp to <c>[0, BeatCount - 1]</c>.</summary>
    public int NearestBeatIndex(double timeSec)
        => BeatPeriodSec > 0
            ? (int)Math.Round((timeSec - FirstDownbeatSec) / BeatPeriodSec)
            : 0;

    /// <summary>Time of the downbeat nearest <paramref name="timeSec"/>. O(1).</summary>
    public double NearestDownbeatSec(double timeSec)
    {
        double bar = BarPeriodSec;
        if (bar <= 0) return 0.0;
        double first = FirstDownbeatSec;
        return first + Math.Round((timeSec - first) / bar) * bar;
    }

    /// <summary>Number of beats <see cref="BeatTimes"/> emits — i.e. the last
    /// valid beat index for this track.
    ///
    /// Walks the grid rather than closing the form, and NOT because that is
    /// easier: the walk accumulates (see <see cref="Walk"/>), so on roughly 2%
    /// of (tempo, phase, duration) inputs the accumulated final bar lands on the
    /// other side of the tail guard from the exactly-multiplied one, and a
    /// closed form is off by one against the array actually emitted. Measured:
    /// 104 disagreements in 4992 sampled cases. A count that can disagree with
    /// the array it counts is worse than an O(bars) loop over a few hundred
    /// items.</summary>
    public int BeatCount => Walk(null, null).Beats;

    /// <summary>Number of downbeats <see cref="DownbeatTimes"/> emits. Same
    /// walk, same reasoning as <see cref="BeatCount"/>.</summary>
    public int DownbeatCount => Walk(null, null).Downbeats;

    /// <summary>Same tempo, phase moved by <paramref name="deltaSec"/>. The grid
    /// nudge controls.</summary>
    public Beatgrid ShiftedBy(double deltaSec) => this with { AnchorSec = AnchorSec + deltaSec };

    /// <summary>Same anchor, new tempo — the spacing stretches and shrinks
    /// around the anchored downbeat, so the kick the user is looking at stays
    /// put while distant ones move. Beats-per-bar is carried, so a width tweak
    /// cannot flip 4/4 to 3/4. A non-positive BPM yields <see cref="Empty"/>'s
    /// period, i.e. an empty grid.</summary>
    public Beatgrid AtBpm(double bpm) => this with { BeatPeriodSec = bpm > 0 ? 60.0 / bpm : 0.0 };

    /// <summary>Same tempo, anchor placed exactly at <paramref name="anchorSec"/>
    /// — i.e. make that instant a downbeat. The "set downbeat here" /
    /// two-point-grid operation.</summary>
    public Beatgrid PivotedAt(double anchorSec) => this with { AnchorSec = anchorSec };

    /// <summary>Recover a grid from already-materialised beat and downbeat
    /// arrays — the adapter for the one boundary that still loses the four
    /// numbers: the version-3 analysis cache, which stores
    /// <see cref="BasicAnalysis"/>'s arrays and nothing else. The arrays are
    /// read back at deck-load time and beats-per-bar has to be recovered from
    /// their spacing because nothing carried it across the cache.
    ///
    /// This is the ONLY re-derivation of beats-per-bar from a synthesised grid
    /// in the codebase, and it exists here — once, at the boundary — rather
    /// than in the deck. It disappears the day the cache stores
    /// (anchor, period, beatsPerBar, duration) directly.
    ///
    /// Note this is NOT the same job as <see cref="BeatgridFitter"/>'s internal
    /// inference: that one takes a majority vote over madmom's noisy RAW
    /// detections, where bars genuinely disagree. These arrays are a synthesised
    /// constant-spacing grid, so a single delta ratio is exact.</summary>
    public static Beatgrid FromGridArrays(
        double[] beatTimes, double[] downbeatTimes, double bpm, double durationSec)
    {
        if (bpm <= 0) return Empty;

        int beatsPerBar = 4;
        if (downbeatTimes.Length >= 2 && beatTimes.Length >= 2)
        {
            double beatPeriod = beatTimes[1] - beatTimes[0];
            double barPeriod = downbeatTimes[1] - downbeatTimes[0];
            if (beatPeriod > 1e-6) beatsPerBar = Math.Max(1, (int)Math.Round(barPeriod / beatPeriod));
        }

        double anchor = downbeatTimes.Length > 0 ? downbeatTimes[0] : 0.0;
        return new Beatgrid(anchor, 60.0 / bpm, beatsPerBar, durationSec);
    }

    /// <summary>Render the grid to the per-beat and per-bar arrays the analysis
    /// cache and the waveform renderer consume. THE array-building loop — the
    /// only one; every other "grid array" in the app comes from here.
    ///
    /// Beats and downbeats are emitted from the same walk so every
    /// <see cref="BeatsPerBar"/>th beat IS a downbeat by construction. That is
    /// what guarantees the waveform's small per-beat ticks land exactly on the
    /// tall bar lines; generating the two independently drifts them apart by a
    /// column or two whenever the anchor does not fall on a raw beat.
    ///
    /// The walk accumulates (<c>db += barPeriod</c>) rather than computing
    /// <c>t0 + k·barPeriod</c>. That is the pre-refactor behaviour preserved
    /// deliberately: the two differ in the last bits, and the arrays this emits
    /// are compared against cached ones.</summary>
    public (double[] Beats, double[] Downbeats) Materialise()
    {
        if (IsEmpty) return ([], []);

        var downbeats = new List<double>(capacity: (int)(DurationSec / BarPeriodSec) + 2);
        var beats = new List<double>(capacity: (int)(DurationSec / BeatPeriodSec) + 2);
        Walk(beats, downbeats);
        return (beats.ToArray(), downbeats.ToArray());
    }

    /// <summary>The one walk over the grid. Appends to whichever lists are
    /// supplied (both may be null, to count only) and returns how many of each
    /// it emitted, so <see cref="Materialise"/> and <see cref="BeatCount"/>
    /// cannot drift apart: there is one loop and one set of tail guards.</summary>
    private (int Beats, int Downbeats) Walk(List<double>? beats, List<double>? downbeats)
    {
        int nBeats = 0, nDownbeats = 0;
        if (IsEmpty) return (0, 0);

        double beatPeriod = BeatPeriodSec;
        double barPeriod = BarPeriodSec;

        for (double db = FirstDownbeatSec; db <= DurationSec + barPeriod / 2; db += barPeriod)
        {
            if (db >= 0) { nDownbeats++; downbeats?.Add(db); }
            // Emit BeatsPerBar beats starting AT this downbeat. The first one IS
            // the downbeat itself; the rest are the intermediate beats.
            for (int i = 0; i < BeatsPerBar; i++)
            {
                double bt = db + i * beatPeriod;
                if (bt >= 0 && bt <= DurationSec + beatPeriod / 2) { nBeats++; beats?.Add(bt); }
            }
        }
        return (nBeats, nDownbeats);
    }

    /// <summary>The per-beat times. Allocates — see <see cref="Materialise"/>.
    /// If you need both arrays, call <see cref="Materialise"/> once instead of
    /// this and <see cref="DownbeatTimes"/>.</summary>
    public double[] BeatTimes() => Materialise().Beats;

    /// <summary>The per-bar (downbeat) times. Allocates — see
    /// <see cref="Materialise"/>.</summary>
    public double[] DownbeatTimes() => Materialise().Downbeats;

    /// <summary>Two-element deconstruction into the materialised
    /// <c>(beats, downbeats)</c> pair — identical to <see cref="Materialise"/>,
    /// and the reason <c>var (beats, downbeats) = fitter.Synthesize…(…)</c>
    /// still reads the way it always did at the array boundary in
    /// <see cref="BasicAnalyzer.ComputeAsync"/>. It ALLOCATES, unlike the
    /// four-element deconstruction the record generates for its own components;
    /// prefer naming <see cref="Materialise"/> where that matters.</summary>
    public void Deconstruct(out double[] Beats, out double[] Downbeats)
        => (Beats, Downbeats) = Materialise();
}
