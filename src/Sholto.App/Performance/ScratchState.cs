namespace Sholto.App.Performance;

/// <summary>One deck's platter-scratch state: velocity accumulator, exponential smoother and
/// release-decay bookkeeping. Owned by <see cref="ScratchEngine"/>, one per deck.</summary>
public sealed class ScratchState
{
    /// <summary>True from the first top-platter tick until the release decay finishes (not just while
    /// ticks are actively arriving).</summary>
    public bool Active;
    /// <summary>Sum of (tick delta x secsPerTick) since the last frame's flush; cleared every frame.</summary>
    public double TickAccum;
    /// <summary>Smoothed signed track-seconds-per-second: what is actually pushed to the deck's ScratchRate.</summary>
    public double Velocity;
    /// <summary>Time of the most recent platter tick.</summary>
    public DateTime LastTickAt = DateTime.MinValue;
    /// <summary>Time of the last frame's flush, for the smoother's dt.</summary>
    public DateTime LastFlushAt = DateTime.MinValue;
    /// <summary>Whether the deck was playing when the platter was grabbed: the release coast's resting rate
    /// (forward speed vs stop).</summary>
    public bool WasPlaying;
    /// <summary>True once the hand has left and the coast is running; the fling launch happens exactly once,
    /// on the tick this flips true.</summary>
    public bool Coasting;
    /// <summary>True while the touch sensor reports a hand on the platter top. Authoritative grab/hold;
    /// release = hand off AND ticks stopped.</summary>
    public bool Touching;
    /// <summary>Decaying peak of the gesture's smoothed velocity (~0.3 s memory); the fling launches from
    /// this, since the platter has physically stopped by the time release is detected.</summary>
    public double PeakVelocity;
    /// <summary>Deceleration of the vinyl brake's friction branch (rate-units/s); set when a brake starts.</summary>
    public double Decel;
    /// <summary>Time constant of the dying tail for this coast: the brake's tail, set when a brake starts.</summary>
    public double TailTau;
    /// <summary>True while a fling is on its timed ease-out coast (see <see cref="ScratchEngine"/>); cleared by a
    /// hand landing, a pause press (hand-over to the brake) and the end of the coast.</summary>
    public bool Timed;
    /// <summary>Seconds of the timed coast already played.</summary>
    public double CoastElapsed;
    /// <summary>Total seconds of the timed coast (the backspin time T).</summary>
    public double CoastSeconds;
    /// <summary>Signed launch gap G above the resting rate at the start of the timed coast.</summary>
    public double CoastGap;
    /// <summary>Resting rate R the timed coast settles at: forward speed for a forward fling on a playing deck, else 0.</summary>
    public double CoastRest;
    /// <summary>True while a vinyl-brake pause is in flight: when the coast reaches rest, the deck is paused
    /// instead of resuming.</summary>
    public bool PauseAtEnd;
}
