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
    /// <summary>Whether the deck was playing when the platter was grabbed: the release coast's target rate
    /// (forward speed vs stop).</summary>
    public bool WasPlaying;
    /// <summary>True once the hand has left and the coast is running; the fling boost fires exactly once,
    /// on the tick this flips true.</summary>
    public bool Coasting;
    /// <summary>True while the touch sensor reports a hand on the platter top. Authoritative grab/hold;
    /// release = hand off AND ticks stopped.</summary>
    public bool Touching;
    /// <summary>Decaying peak of the gesture's smoothed velocity (~0.3 s memory); the fling launches from
    /// this, since the platter has physically stopped by the time release is detected.</summary>
    public double PeakVelocity;
    /// <summary>Deceleration for this coast (rate-units/s): the scratch friction constant normally, or the
    /// gentler vinyl-brake rate when <see cref="PauseAtEnd"/> is set.</summary>
    public double Decel;
    /// <summary>Time constant of the dying tail for this coast: the option default, shortened for a spinback.</summary>
    public double TailTau;
    /// <summary>True while a vinyl-brake pause is in flight: when the coast reaches rest, the deck is paused
    /// instead of resuming.</summary>
    public bool PauseAtEnd;
}
