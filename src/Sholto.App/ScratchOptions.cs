namespace Sholto.App;

/// <summary>Platter feel: every number that decides how the DDJ-FLX4 jog wheel
/// scrubs, scratches, flings and brakes, supplied via the standard
/// <c>IOptions&lt;ScratchOptions&gt;</c> pipeline like <see cref="MagnetismOptions"/>.
/// Defaults live here so tuning is one edit in one place; nothing in the
/// orchestrator carries its own magic numbers for the wheel.
///
/// Rates are multiples of normal playback speed (1.0 = forward at the deck's
/// tempo, negative = reverse).</summary>
public sealed class ScratchOptions
{
    // --- Seek (jog) ------------------------------------------------------------

    /// <summary>Track-seconds moved per top-platter tick when the platter is
    /// used as a seek (Shift fast-search, or a deck that can't scratch yet).</summary>
    public double TopPlatterSecsPerTick { get; set; } = 0.05;

    /// <summary>Track-seconds moved per side-ring tick. The side ring is the
    /// slow, precise nudge; the top platter is the fast one.</summary>
    public double SideRingSecsPerTick { get; set; } = 0.00125;

    // --- Scratch (hand on the platter) -------------------------------------------

    /// <summary>Scratch sensitivity: track-seconds of platter travel per tick,
    /// turned into a playback rate (accumulated seconds ÷ elapsed wall time).
    /// Much finer than the seek constant — the jog emits ~1000+ ticks/s. ~0.004
    /// puts a natural spin near 1× playback.</summary>
    public double ScratchSecsPerTick { get; set; } = 0.004;

    /// <summary>No tick for this long → the hand has left the platter.</summary>
    public double ReleaseIdleMs { get; set; } = 80;

    /// <summary>Low-pass on the raw tick velocity. Ticks land in bursts at the
    /// controller's poll rate and are flushed once per 60 Hz frame, so the raw
    /// velocity stair-steps; a short smoothing hides that without adding lag.</summary>
    public double SmoothingTauSec { get; set; } = 0.02;

    // --- Release: resume vs fling -------------------------------------------------

    /// <summary>The line between "scrubbing" and "spun it". Peak |rate| during the
    /// gesture at or above this = a fling: the deck coasts on for the backspin
    /// time and distance (the backspin effect). Below it the deck resumes instantly from exactly where
    /// the hand let go. Raise if fast rewinds coast when they shouldn't; lower
    /// if real spins die on release.</summary>
    public double FlingThreshold { get; set; } = 3.0;

    /// <summary>Exponent p of the fling's timed ease-out, <c>v(t) = R + G·(1 − t/T)^p</c>. 2 = fast whoosh at
    /// release and a soft landing; higher is a sharper lurch then a longer crawl.</summary>
    public double CoastShape { get; set; } = 2.0;

    /// <summary>Peak |rate| at which fling strength is 1. Strength g = sqrt(|peak| / this), clamped, scales the
    /// backspin distance, so a harder fling goes further in the same time.</summary>
    public double FlingReferencePeak { get; set; } = 10.0;

    /// <summary>Lowest fling strength g (a fling just over the threshold).</summary>
    public double FlingStrengthMin { get; set; } = 0.5;

    /// <summary>Highest fling strength g (the cap on how much harder flings add).</summary>
    public double FlingStrengthMax { get; set; } = 2.0;

    /// <summary>Cap on the launch rate above rest, so short times with long distances cannot ask for silly
    /// rates; when it bites the spin keeps its time but falls short of its distance.</summary>
    public double MaxLaunchRate { get; set; } = 40.0;

    /// <summary>Tempo used to turn beats into seconds when the track has no analysed BPM.</summary>
    public double FallbackBpm { get; set; } = 120.0;

    /// <summary>Vinyl brake only: below this speed-gap to rest, the brake switches from
    /// constant friction to an exponential glide, so it draws out instead of stopping on a dime.</summary>
    public double CoastKnee { get; set; } = 2.0;

    /// <summary>Time constant of the vinyl brake's dying tail.</summary>
    public double CoastTailTauSec { get; set; } = 0.175;

    // --- Diagnostics and recency ---------------------------------------------------

    /// <summary>SHOLTO_SCRATCH_LOG=1 prints per-frame scratch velocity and play position,
    /// so the platter's actual motion (and any position jumps) are visible in the log.</summary>
    public bool Log { get; set; } = Environment.GetEnvironmentVariable("SHOLTO_SCRATCH_LOG") == "1";

    /// <summary>How recently a jog event must have arrived for a deck to count as
    /// "actively being adjusted right now" (the IsScrubbing window, and the
    /// both-decks-jogging check the magnet uses).</summary>
    public TimeSpan ActiveJogWindow { get; set; } = TimeSpan.FromMilliseconds(250);

    // --- Brake ----------------------------------------------------------------------

    /// <summary>How long the vinyl brake (pause while playing) takes to spin a
    /// deck at unity down to a stop.</summary>
    public double BrakeSeconds { get; set; } = 0.45;
}
