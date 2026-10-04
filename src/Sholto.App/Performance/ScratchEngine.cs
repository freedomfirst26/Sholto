using Microsoft.Extensions.Options;
using Sholto.Data;
using Sholto.App.Decks;

namespace Sholto.App.Performance;

/// <summary>Platter physics, one <see cref="ScratchState"/> per deck. Every platter-feel number lives in
/// <see cref="ScratchOptions"/>. Pause on a scratch-capable deck is a vinyl brake: the scratch coast rides
/// down to zero, THEN pauses; a second press mid-brake cancels and spins back to normal playback.
/// All methods run on the app thread; <see cref="Turn"/> and <see cref="Touch"/> allocate nothing.</summary>
public sealed class ScratchEngine : IScratchEngine
{
    private readonly IDecks _decks;
    private readonly IPlaybackRequests _requests;
    private readonly IJogRecencyWriter _recency;
    private readonly IFrameClock _clock;
    private readonly ScratchOptions _options;
    private readonly ScratchState _state1 = new();
    private readonly ScratchState _state2 = new();

    public ScratchEngine(IDecks decks, IPlaybackRequests requests, IJogRecencyWriter recency,
                         IFrameClock clock, IOptions<ScratchOptions> options)
    {
        _decks = decks;
        _requests = requests;
        _recency = recency;
        _clock = clock;
        _options = options.Value;
        _requests.BrakePauseRequested += OnBrakePauseRequested;
    }

    public ScratchState StateOf(int deck) => deck == 0 ? _state1 : _state2;

    public void Dispose() => _requests.BrakePauseRequested -= OnBrakePauseRequested;

    private void OnBrakePauseRequested(int deck)
    {
        var st = StateOf(deck);
        var session = _decks.DeckFor(deck);

        if (st.Active && st.PauseAtEnd)
        {
            // Second press mid-brake: cancel — hand the provider back at normal
            // speed and keep playing (press-pause-press = "changed my mind").
            session.Scratch.EndScratch();
            session.IsScratching = false;
            st.Active = false;
            st.PauseAtEnd = false;
            st.LastFlushAt = DateTime.MinValue;
            return;
        }

        if (st.Active)
        {
            // Pause pressed MID-SCRATCH (backspin still coasting, or hand still
            // on the platter): don't kill the spin — let it finish its motion,
            // just park instead of resuming when it comes to rest. Velocity and
            // friction are left untouched; only the landing changes.
            st.PauseAtEnd = true;
            st.WasPlaying = false;   // coast target → 0 (rest), not forward speed
            return;
        }

        st.Active = true;
        st.Coasting = true;                 // skip the fling boost — this is a brake
        st.PauseAtEnd = true;
        st.WasPlaying = false;              // coast target = 0 (spin down to a stop)
        st.Velocity = session.Tempo.PlaybackSpeed;
        st.Decel = 1.0 / _options.BrakeSeconds;      // unity → 0 in ~BrakeSeconds
        st.TailTau = _options.CoastTailTauSec;
        st.PeakVelocity = 0;
        st.LastTickAt = DateTime.MinValue;  // no "recent ticks" → straight to the coast branch
        session.IsScratching = true;         // suppress magnetism during the brake
    }

    /// <summary>Inspect mode ended: repair any platter grab it stranded. Turning Inspect on needs nothing here.</summary>
    public void Handle(in InspectModeChanged e)
    {
        if (!e.On) ReleaseStrandedTouches();
    }

    /// <summary>While the guide is open the Inspect gate echoes the controller's commands instead of running
    /// them, so if a hand was already resting on a top platter when Inspect opened, the eventual lift never
    /// reaches <see cref="Touch"/>. That leaves <see cref="ScratchState.Touching"/> stuck true forever:
    /// <see cref="Tick"/> keeps treating the hand as "on" and writes <c>ScratchRate(0)</c> every frame,
    /// silencing that deck until the platter is touched and released again in Play mode. Called when Inspect
    /// ends, so a lift that never arrived is treated as if it just had; the normal release-decay path takes
    /// it from there. Deliberately touches nothing else in the state (velocity, decel, coasting).</summary>
    public void ReleaseStrandedTouches()
    {
        if (_state1.Active) _state1.Touching = false;
        if (_state2.Active) _state2.Touching = false;
    }

    public void Touch(int deck, bool touching, bool shifted)
    {
        var session = _decks.DeckFor(deck);
        var st = StateOf(deck);
        st.Touching = touching;
        // Hand lands: grab now, at rate = the deck's current speed, so
        // the sound holds under the finger (rate → 0 as no ticks
        // arrive) rather than waiting for the first tick. Shift + touch
        // is the silent fast-search, not a grab; a brake in flight
        // keeps its own state.
        if (touching && !st.Active && !shifted && session.Scratch.CanScratch)
        {
            st.Active = true;
            st.PauseAtEnd = false;
            st.Decel = _options.DecelPerSec;
            st.TailTau = _options.CoastTailTauSec;
            st.WasPlaying = session.Loading.IsPlaying;
            st.Velocity = st.WasPlaying ? session.Tempo.PlaybackSpeed : 0;
            st.PeakVelocity = 0;
            session.IsScratching = true;
            if (_options.Log)
                Console.WriteLine($"[scratch] TOUCH deck={deck} playing={st.WasPlaying} pos={session.Playhead.PlayPosition:F3}");
        }
        else if (!touching && _options.Log)
            Console.WriteLine($"[scratch] LIFT  deck={deck} v={st.Velocity:F2}");
    }

    public void Turn(int deck, int delta)
    {
        var session = _decks.DeckFor(deck);
        var st = StateOf(deck);
        if (!st.Active)
        {
            st.Active = true;
            st.PauseAtEnd = false;
            st.Decel = _options.DecelPerSec;
            st.TailTau = _options.CoastTailTauSec;
            st.WasPlaying = session.Loading.IsPlaying;
            // Start from the deck's actual current rate, not 0 — a
            // grab on a playing deck shouldn't hiccup to silence
            // before the hand's motion takes over.
            st.Velocity = st.WasPlaying ? session.Tempo.PlaybackSpeed : 0;
            session.IsScratching = true;
            if (_options.Log)
                Console.WriteLine($"[scratch] GRAB deck={deck} playing={st.WasPlaying} pos={session.Playhead.PlayPosition:F3}");
        }
        st.TickAccum += delta * _options.ScratchSecsPerTick;
        st.LastTickAt = _clock.Now;
    }

    /// <summary>Per-deck: turn accumulated top-platter ticks into a smoothed
    /// signed varispeed rate and push it into the deck (ScratchRate), or —
    /// once ticks stop arriving — decay that rate back to the deck's resting
    /// rate and hand the provider back (EndScratch). No-op while the platter
    /// hasn't been touched (<see cref="ScratchState.Active"/> false).</summary>
    public void Tick(int deck, DateTime now)
    {
        var st = StateOf(deck);
        if (!st.Active) return;
        var session = _decks.DeckFor(deck);

        double dt = st.LastFlushAt == DateTime.MinValue ? 1.0 / 60.0 : (now - st.LastFlushAt).TotalSeconds;
        if (dt <= 0) dt = 1.0 / 60.0;
        st.LastFlushAt = now;

        // Hand on = the touch sensor says so, OR ticks are still arriving (a
        // flung platter keeps ticking after the hand has lifted — that free
        // spin is the real backspin, so it counts as "still driving"). The
        // tick-gap timeout is only a fallback now, not the release detector.
        bool tickedThisWindow = (now - st.LastTickAt).TotalMilliseconds < _options.ReleaseIdleMs;
        bool handOn = st.Touching || tickedThisWindow;

        if (handOn)
        {
            // Grabbed and moving: exponential low-pass of the raw instantaneous
            // velocity (sum of this frame's tick deltas ÷ elapsed time) toward
            // the smoothed value Deck actually plays at.
            double rawVelocity = st.TickAccum / dt;
            st.TickAccum = 0;
            double alpha = 1 - Math.Exp(-dt / _options.SmoothingTauSec);
            st.Velocity += (rawVelocity - st.Velocity) * alpha;
            st.Coasting = false;   // hand is back on — re-arm the fling detector
            // Peak-hold the gesture's velocity (decaying, ~0.3 s memory). The
            // FLX4 platter physically stops WHILE still ticking, so by the time
            // release is detected the smoothed velocity has already died — the
            // fling must launch from the rip's peak speed, not its last gasp.
            double peakDecay = Math.Exp(-dt / 0.3);
            st.PeakVelocity *= peakDecay;
            if (Math.Abs(st.Velocity) > Math.Abs(st.PeakVelocity))
                st.PeakVelocity = st.Velocity;
            session.Scratch.ScratchRate(st.Velocity);
            if (_options.Log)
                Console.WriteLine($"[scratch] MOVE v={st.Velocity,7:F2} pos={session.Playhead.PlayPosition,7:F3}");
        }
        else
        {
            // Let go: coast under constant friction straight toward the deck's
            // resting rate (its own forward speed if it was playing when grabbed,
            // else 0). Constant deceleration means a hard backspin fling keeps
            // real momentum — audible whoosh over seconds and bars — while a
            // small nudge is back at normal speed in ~0.1 s with no slow-mo
            // lull. The deck lands wherever the platter coasts to — no snapping.
            // First coast frame: if the hand left the platter at fling speed,
            // project the momentum (see FlingBoost) before friction takes it.
            double target = st.WasPlaying ? session.Tempo.PlaybackSpeed : 0.0;
            if (!st.Coasting)
            {
                st.Coasting = true;
                if (Math.Abs(st.PeakVelocity) >= _options.FlingThreshold)
                {
                    // A genuine fling: launch the coast from the gesture's PEAK
                    // speed (see peak-hold above), boosted — not from the
                    // smoothed velocity, which has already decayed by the time
                    // the platter physically stopped.
                    st.Velocity = st.PeakVelocity * _options.FlingBoost;
                    // A spinback dies out faster than a forward fling: more
                    // friction and a shorter tail, so it completes in 1/N the time.
                    if (st.Velocity < 0)
                    {
                        st.Decel *= _options.SpinbackSpeedup;
                        st.TailTau = _options.CoastTailTauSec / _options.SpinbackSpeedup;
                    }
                    if (_options.Log)
                        Console.WriteLine($"[scratch] FLING peak={st.PeakVelocity:F2} → v={st.Velocity:F2}");
                }
                else
                {
                    // Hand-guided scrub or rewind: no momentum. The deck resumes
                    // from exactly where the hand left it — coasting on would
                    // carry it a further stretch back before resuming, which
                    // reads as "it jumped to an earlier point" rather than
                    // "it continued". Snap the velocity to the resting rate so
                    // the END branch below fires this same frame.
                    st.Velocity = target;
                }
                st.PeakVelocity = 0;
            }
            // A backspin on a playing deck glides to REST (0) — the drawn-out
            // reverse tail — and then EndScratch below resumes forward playback
            // INSTANTLY. Gliding all the way to +PlaybackSpeed would crawl
            // audibly through slow-motion on the way back up; real decks resume
            // crisply the moment the reverse motion dies.
            double glideTarget = target > 0 && st.Velocity < -0.02 ? 0.0 : target;
            double gap = Math.Abs(st.Velocity - glideTarget);
            if (gap > _options.CoastKnee)
            {
                // Fast phase: constant friction, real momentum.
                double step = st.Decel * dt;
                if (st.Velocity < glideTarget) st.Velocity = Math.Min(glideTarget, st.Velocity + step);
                else                           st.Velocity = Math.Max(glideTarget, st.Velocity - step);
            }
            else
            {
                // Tail: below the knee, ease exponentially into rest — the last
                // stretch of a backspin draws out and dies away instead of
                // stopping on a dime.
                double a = 1 - Math.Exp(-dt / st.TailTau);
                st.Velocity += (glideTarget - st.Velocity) * a;
            }

            if (Math.Abs(st.Velocity - glideTarget) < 0.02)
            {
                session.Scratch.EndScratch();
                if (st.PauseAtEnd)
                {
                    // Vinyl brake finished: the platter has "stopped" — now pause.
                    session.Transport.Pause();
                    st.PauseAtEnd = false;
                }
                session.IsScratching = false;
                st.Active = false;
                st.Coasting = false;
                st.LastFlushAt = DateTime.MinValue;
                // A scratch is NOT a jog: expire the jog-recency stamps so the
                // magnetic Quantize() (armed by "recently jogged, now idle")
                // doesn't fire ~180 ms after release and SeekRelative the deck
                // up to half a beat — the post-release hop to "a place the
                // timeline wasn't". The deck stays exactly where it coasted to.
                _recency.ClearAfterScratchEnd(deck == 0);
                if (_options.Log)
                    Console.WriteLine($"[scratch] END  pos={session.Playhead.PlayPosition,7:F3}");
            }
            else
            {
                if (_options.Log)
                    Console.WriteLine($"[scratch] COAST v={st.Velocity,7:F2} pos={session.Playhead.PlayPosition,7:F3}");
                session.Scratch.ScratchRate(st.Velocity);
            }
        }
    }
}
