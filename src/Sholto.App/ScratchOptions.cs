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
    /// gesture at or above this = a fling: the deck coasts on with momentum (the
    /// backspin effect). Below it the deck resumes instantly from exactly where
    /// the hand let go. Raise if fast rewinds coast when they shouldn't; lower
    /// if real spins die on release.</summary>
    public double FlingThreshold { get; set; } = 3.0;

    /// <summary>Multiplier on the release velocity of a fling. The FLX4's platter
    /// has no flywheel — it stops almost the instant the hand leaves — so a
    /// physical rip reads weak; this projects the momentum a weighted platter
    /// would carry. 1.0 = none. (4.0 was hilariously too much.)</summary>
    public double FlingBoost { get; set; } = 1.5;

    /// <summary>How much faster a spinback (a fling in reverse) dies out than a
    /// forward fling. Scales the friction up and the dying tail down, so the
    /// same spin comes to rest in 1/this of the time and covers 1/this of the
    /// track. 2.0 = the spinback completes twice as fast.</summary>
    public double SpinbackSpeedup { get; set; } = 2.0;

    /// <summary>Constant deceleration of a fling, in rate-units per second — like
    /// a real platter under friction. Lower = longer coast. A hard backspin
    /// (|rate| ~ 20) covers bars before settling; a small one is back at speed
    /// in ~0.1 s.</summary>
    public double DecelPerSec { get; set; } = 12.0;

    /// <summary>Below this speed-gap to the resting rate, a fling switches from
    /// constant friction to an exponential glide — the drawn-out dying tail of
    /// a backspin instead of stopping on a dime.</summary>
    public double CoastKnee { get; set; } = 2.0;

    /// <summary>Time constant of that dying tail.</summary>
    public double CoastTailTauSec { get; set; } = 0.175;

    // --- Brake ----------------------------------------------------------------------

    /// <summary>How long the vinyl brake (pause while playing) takes to spin a
    /// deck at unity down to a stop.</summary>
    public double BrakeSeconds { get; set; } = 0.45;
}
