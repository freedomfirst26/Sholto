using Avalonia.Threading;
using Microsoft.EntityFrameworkCore;
using Sholto.Analysis;
using Sholto.Audio;
using Sholto.Controller;
using Sholto.Storage;
using Sholto.App.ViewModels;

namespace Sholto.App;

/// <summary>The live-mix coordinator — the orchestration layer under App. Owns the
/// input→action routing (controller events → deck/mixer actions), the jog-wheel
/// coalescing, and the 60 Hz tick that flushes jog, runs magnetic beat-snap, and
/// syncs playheads. These belong together (they share jog + magnetism state), so
/// they live here as one cohesive object rather than scattered across the Avalonia
/// App class. App becomes a plain composition root that feeds this events + a Tick.
///
/// All methods run on the UI thread (the caller marshals controller events there).</summary>
public sealed class Orchestrator : IDisposable
{
    private readonly MainViewModel _vm;
    private readonly Func<IDbContextFactory<SholtoDbContext>?> _dbFactory;

    // Jog-wheel scrubs are coalesced per frame so we issue one Seek per deck per ~16 ms.
    private double _pendingJog1, _pendingJog2;
    // Calibration: how many track-seconds one tick of jog rotation represents.
    // Top-platter ticks are coarser (fast scrub / scratch surface); the side
    // ring is fine-grained nudging. Shared by the silent-seek fallback path
    // and the scratch velocity accumulator below — see JogRotated.
    private const double TopPlatterSecsPerTick = 0.05;
    private const double SideRingSecsPerTick = 0.00125;
    // Scratch sensitivity: track-seconds of platter travel per jog tick, used to
    // turn the tick stream into a playback RATE (rate = accumulated secs ÷ elapsed
    // wall-time). This is MUCH finer than the seek constant above — the jog emits
    // ~1000+ ticks/s, so reusing 0.05 here drove the rate to ~30× (chipmunk/tinny
    // and the position flew). ~0.001 puts a natural spin near 1× playback. It's the
    // scratch feel knob — override live with SHOLTO_SCRATCH_SENS to dial it in.
    private static readonly double ScratchSecsPerTick =
        double.TryParse(Environment.GetEnvironmentVariable("SHOLTO_SCRATCH_SENS"), out var s) && s > 0
            ? s : 0.004;
    // SHOLTO_SCRATCH_LOG=1 → print per-frame scratch velocity + play position, so
    // the platter's actual motion (and any position jumps) are visible in the log.
    private static readonly bool _scratchLog =
        Environment.GetEnvironmentVariable("SHOLTO_SCRATCH_LOG") == "1";
    // Per-deck platter-scratch state (top platter only — see ScratchState).
    private readonly ScratchState[] _scratch = { new(), new() };
    // Per-deck Shift modifier state — the FLX-4 emits Shift+arrow without a deck, so
    // NudgeGrid uses this to route.
    private readonly bool[] _shiftHeld = new bool[2];
    // Stem-level modifier: while held, the EQ knobs become per-stem attenuators.
    private bool _stemLevelMode;
    // Browse long-press: hold ~1 s to force-reanalyze the highlighted track.
    private DispatcherTimer? _browseHoldTimer;
    private DispatcherTimer? _positionTimer;

    public Orchestrator(MainViewModel vm, Func<IDbContextFactory<SholtoDbContext>?> dbFactory)
    {
        _vm = vm;
        _dbFactory = dbFactory;
        // Double-clicking a library row re-analyzes it — same path as the browse
        // long-press. The VM only raises the request; we hold the provider + factory.
        _vm.ReanalyzeSelectedRequested += OnReanalyzeSelectedRequested;

        // Deck transport → controller output: each deck raises the resolved LED state
        // (solid while Playing, blinking while Ending, off while Stopped — the deck
        // owns the flash clock so the LED stays in lockstep with its disc ring). We
        // forward it to that deck's BEAT SYNC LED (App wires BeatSyncLightRequested to
        // Controller.SetBeatSync).
        _vm.Deck1.DeckLightChanged += on => BeatSyncLightRequested?.Invoke(0, on);
        _vm.Deck2.DeckLightChanged += on => BeatSyncLightRequested?.Invoke(1, on);

        // Pause on a scratch-capable deck = vinyl brake: ride the scratch coast
        // down to zero (pitch falling like a stopping turntable), THEN pause —
        // instead of cutting to stone silence. Second press mid-brake cancels
        // and spins back to normal playback.
        _vm.BrakePauseRequested += OnBrakePauseRequested;
    }

    // How long the vinyl brake takes to stop a deck playing at unity. Tunable
    // with SHOLTO_BRAKE_SEC.
    private static readonly double BrakeSeconds =
        double.TryParse(Environment.GetEnvironmentVariable("SHOLTO_BRAKE_SEC"), out var b) && b > 0
            ? b : 0.45;

    private void OnBrakePauseRequested(int deck)
    {
        var st = _scratch[deck];
        var deckVm = _vm.DeckFor(deck);

        if (st.Active && st.PauseAtEnd)
        {
            // Second press mid-brake: cancel — hand the provider back at normal
            // speed and keep playing (press-pause-press = "changed my mind").
            deckVm.Player.EndScratch();
            deckVm.IsScratching = false;
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
        st.Velocity = deckVm.Player.PlaybackSpeed;
        st.Decel = 1.0 / BrakeSeconds;      // unity → 0 in ~BrakeSeconds
        st.PeakVelocity = 0;
        st.LastTickAt = DateTime.MinValue;  // no "recent ticks" → straight to the coast branch
        deckVm.IsScratching = true;         // suppress magnetism during the brake
    }

    /// <summary>Raised to request a deck's BEAT SYNC LED be set (deck index, on/off).
    /// The App forwards this to the controller.</summary>
    public event Action<int, bool>? BeatSyncLightRequested;

    /// <summary>Raised when a stem-mute pad's active state changes — App forwards
    /// this to the controller's pad LED. Args: deck, stem group (0=Drums,
    /// 1=Vocals, 2=Instrumental), new on/off state.</summary>
    public event Action<int, int, bool>? PadLightRequested;

    /// <summary>Raised when MASTER CUE is toggled — App forwards it to the audio
    /// engine's master-cue monitor. Bool is the new on/off state.</summary>
    public event Action<bool>? MasterCueRequested;

    private void OnReanalyzeSelectedRequested() => ReanalyzeHighlighted("double-click");

    /// <summary>Force-reanalyze the highlighted library track (BPM/beats/peaks + key)
    /// via the deck's AnalysisProvider, writing through every cache tier. Shared by the
    /// browse-knob long-press and the library double-click.</summary>
    private void ReanalyzeHighlighted(string source)
    {
        var vm = _vm;
        var provider = vm.Deck1.Player.AnalysisProvider;
        if (provider is null) { Console.WriteLine($"[Orchestrator] {source} re-analyze: no AnalysisProvider yet"); return; }
        Console.WriteLine($"[Orchestrator] {source} → re-analyzing {vm.SelectedTrack?.FilePath}");
        var factory = _dbFactory();
        _ = vm.OnBrowseHeldAsync(
            t => AudioFileDecoder.Decode(t.FilePath),
            provider,
            saveKey: factory is not null
                ? (path, key) => new KeyAnalysisCache(factory).PutAsync(path, key)
                : null);
    }

    /// <summary>Start the 60 Hz tick (jog flush + magnetism + playhead sync).</summary>
    public void Start()
    {
        _positionTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(16) };
        _positionTimer.Tick += (_, _) => Tick();
        _positionTimer.Start();
    }

    /// <summary>Translate one controller event into deck / mixer actions. Called on
    /// the UI thread.</summary>
    public void HandleControllerEvent(ControllerEvent evt)
    {
        var vm = _vm;
        switch (evt)
        {
            case ControllerEvent.BrowseRotated r:
                vm.OnBrowseRotated(r.Delta);
                break;
            case ControllerEvent.BrowsePressed:
                // Short tap: no-op (Load 1 / Load 2 buttons do the loading). Long
                // press (≥1 s): force-reanalyze the highlighted track. Some
                // controllers retransmit NoteOn while held; leave a running timer be.
                if (_browseHoldTimer is null)
                {
                    var timer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(1) };
                    timer.Tick += (_, _) =>
                    {
                        timer.Stop();
                        if (ReferenceEquals(_browseHoldTimer, timer)) _browseHoldTimer = null;
                        ReanalyzeHighlighted("browse-hold");
                    };
                    _browseHoldTimer = timer;
                    timer.Start();
                }
                break;
            case ControllerEvent.BrowseReleased:
                _browseHoldTimer?.Stop();
                _browseHoldTimer = null;
                break;
            case ControllerEvent.LoadToDeck l:
            {
                var sel = vm.SelectedTrack;
                if (sel is not null)
                {
                    var deck = vm.DeckFor(l.Deck);
                    var mult = vm.GetBpmMultiplierFor(sel.FilePath);
                    deck.BeginLoad(sel, mult);
                    _ = Task.Run(async () =>
                    {
                        var samples = AudioFileDecoder.Decode(sel.FilePath);
                        await Dispatcher.UIThread.InvokeAsync(() =>
                            deck.LoadTrack(sel, sel.FilePath, samples, mult));
                    });
                }
                break;
            }
            case ControllerEvent.PlayPressed p:
                vm.OnPlayPressed(p.Deck);
                break;
            case ControllerEvent.CrossfaderMoved c:
                vm.Crossfader = c.Position;
                break;
            case ControllerEvent.ChannelVolumeMoved v:
                vm.DeckFor(v.Deck).ChannelGain = v.Value;
                break;
            case ControllerEvent.CueChanged cc:
                vm.DeckFor(cc.Deck).CueActive = cc.On;
                break;
            case ControllerEvent.MasterCueChanged mc:
                // MASTER CUE monitor on/off (state owned by the controller button).
                MasterCueRequested?.Invoke(mc.On);
                break;
            case ControllerEvent.EqMoved e:
            {
                // While the stem-level button is held, the 3 EQ knobs on both decks
                // become stem-group attenuators (HI → Drums, MID → Vocals, LOW → Inst).
                if (_stemLevelMode)
                {
                    var deckVm = vm.DeckFor(e.Deck);
                    switch (e.Band)
                    {
                        case EqBand.High: deckVm.DrumsLevel        = e.Value; break;
                        case EqBand.Mid:  deckVm.VocalsLevel       = e.Value; break;
                        default:          deckVm.InstrumentalLevel = e.Value; break;
                    }
                }
                else
                {
                    vm.DeckFor(e.Deck).Player.SetEq((int)e.Band, e.Value);
                }
                break;
            }
            case ControllerEvent.FilterMoved f:
                vm.DeckFor(f.Deck).Player.SetFilter(f.Position);
                break;
            case ControllerEvent.TempoMoved t:
                vm.DeckFor(t.Deck).SetTempoPosition(t.Position);
                break;
            case ControllerEvent.StemToggle st:
            {
                var deckVm = vm.DeckFor(st.Deck);
                bool nextActive = st.Group switch
                {
                    0 => !deckVm.DrumsActive,
                    1 => !deckVm.VocalsActive,
                    _ => !deckVm.InstrumentalActive,
                };
                switch (st.Group)
                {
                    case 0: deckVm.DrumsActive        = nextActive; break;
                    case 1: deckVm.VocalsActive       = nextActive; break;
                    case 2: deckVm.InstrumentalActive = nextActive; break;
                }
                deckVm.Player.SetStemGroup(st.Group, nextActive);
                PadLightRequested?.Invoke(st.Deck, st.Group, nextActive);
                break;
            }
            case ControllerEvent.BeatLoopToggle bl:
                vm.DeckFor(bl.Deck).Player.EnableBeatLoop(bl.Bars);
                break;
            case ControllerEvent.BeatLoopHalve bh:
                vm.DeckFor(bh.Deck).Player.HalveLoop();
                break;
            case ControllerEvent.BeatLoopDouble bd:
                vm.DeckFor(bd.Deck).Player.DoubleLoop();
                break;
            case ControllerEvent.DeckShift ds:
                _shiftHeld[ds.Deck] = ds.Pressed;
                break;
            case ControllerEvent.StemLevelMode sm:
                _stemLevelMode = sm.Pressed;
                break;
            case ControllerEvent.BeatSyncPressed:
                // Plain BEAT SYNC — beat-sync not yet implemented.
                break;
            case ControllerEvent.CyclePitchRange cpr:
                vm.DeckFor(cpr.Deck).CyclePitchRange();
                break;
            case ControllerEvent.NudgeGrid n:
            {
                if (n.Deck >= 0) { vm.DeckFor(n.Deck).Player.NudgeGrid(n.Beats); break; }
                // Shift+Left/Right arrives deck-less; route via held Shift, else the
                // active-loop deck, else deck 0.
                int target;
                if (_shiftHeld[0]) target = 0;
                else if (_shiftHeld[1]) target = 1;
                else if (vm.DeckFor(0).Player.ActiveLoop is not null) target = 0;
                else if (vm.DeckFor(1).Player.ActiveLoop is not null) target = 1;
                else target = 0;
                vm.DeckFor(target).Player.NudgeGrid(n.Beats);
                break;
            }
            case ControllerEvent.JogRotated j:
            {
                // Loop locked: the jog wheel is ignored while a loop is active, else
                // scrubbing could pull the playhead outside the loop and break the wrap.
                var deckVm = vm.DeckFor(j.Deck);
                if (deckVm.Player.ActiveLoop is not null) break;

                // Shift + top platter = fast search: silent 2× seek through the
                // track (CDJ Shift+jog), bypassing the audible scratch entirely.
                if (j.Source == JogSource.TopPlatter && _shiftHeld[j.Deck])
                {
                    double fastSecs = j.Delta * TopPlatterSecsPerTick * 2;
                    if (j.Deck == 0) _pendingJog1 += fastSecs;
                    else             _pendingJog2 += fastSecs;
                    vm.LastJoggedDeck = j.Deck == 0 ? 1 : 2;
                    vm.LastJogAt = DateTime.UtcNow;
                    if (j.Deck == 0) vm.LastJogAt1 = vm.LastJogAt;
                    else             vm.LastJogAt2 = vm.LastJogAt;
                    break;
                }

                // Top platter on a scratch-capable deck: route into the scratch
                // velocity accumulator instead of the silent-seek pipeline —
                // Tick() turns this into an audible varispeed rate rather than a
                // Seek. Side ring always keeps the old nudge behaviour, and the
                // top platter falls back to it too on a deck that can't scratch
                // yet (still on the streaming/pre-decode provider).
                if (j.Source == JogSource.TopPlatter && deckVm.Player.CanScratch)
                {
                    var st = _scratch[j.Deck];
                    if (!st.Active)
                    {
                        st.Active = true;
                        st.PauseAtEnd = false;
                        st.Decel = ScratchDecelPerSec;
                        st.WasPlaying = deckVm.Player.IsPlaying;
                        // Start from the deck's actual current rate, not 0 — a
                        // grab on a playing deck shouldn't hiccup to silence
                        // before the hand's motion takes over.
                        st.Velocity = st.WasPlaying ? deckVm.Player.PlaybackSpeed : 0;
                        deckVm.IsScratching = true;
                        if (_scratchLog)
                            Console.WriteLine($"[scratch] GRAB deck={j.Deck} playing={st.WasPlaying} pos={deckVm.Player.PlayPosition:F3}");
                    }
                    st.TickAccum += j.Delta * ScratchSecsPerTick;
                    st.LastTickAt = DateTime.UtcNow;
                }
                else
                {
                    // Accumulate; Tick() flushes it into one Seek per frame — each Seek
                    // flushes SoundFlow's buffer, so per-event seeks (~100/s) would glitch.
                    double secsPerTick = j.Source == JogSource.TopPlatter ? TopPlatterSecsPerTick : SideRingSecsPerTick;
                    if (j.Deck == 0) _pendingJog1 += j.Delta * secsPerTick;
                    else             _pendingJog2 += j.Delta * secsPerTick;
                }
                vm.LastJoggedDeck = j.Deck == 0 ? 1 : 2;
                var nowUtc = DateTime.UtcNow;
                vm.LastJogAt = nowUtc;
                if (j.Deck == 0) vm.LastJogAt1 = nowUtc;
                else             vm.LastJogAt2 = nowUtc;
                break;
            }
        }
    }

    /// <summary>60 Hz: flush coalesced jog into one Seek per deck (scaled down by
    /// magnetic beat-snap), mark scrubbing, update magnetism, sync playheads.</summary>
    public void Tick()
    {
        var vm = _vm;
        double scale = 1 - vm.MagnetismFactor * 0.9;
        if (_pendingJog1 != 0) { vm.Deck1.Player.SeekRelative(_pendingJog1 * scale); _pendingJog1 = 0; }
        if (_pendingJog2 != 0) { vm.Deck2.Player.SeekRelative(_pendingJog2 * scale); _pendingJog2 = 0; }

        var now = DateTime.UtcNow;
        vm.Deck1.IsScrubbing = vm.LastJoggedDeck == 1 && (now - vm.LastJogAt) < TimeSpan.FromMilliseconds(250);
        vm.Deck2.IsScrubbing = vm.LastJoggedDeck == 2 && (now - vm.LastJogAt) < TimeSpan.FromMilliseconds(250);

        TickScratch(vm.Deck1, _scratch[0], now);
        TickScratch(vm.Deck2, _scratch[1], now);

        vm.UpdateMagnetism();

        if (vm.Deck1.Player.IsLoaded) vm.Deck1.SyncPlayPosition();
        if (vm.Deck2.Player.IsLoaded) vm.Deck2.SyncPlayPosition();
    }

    // How long a platter's velocity signal is smoothed over. Ticks land in
    // bursts at the controller's poll rate and get flushed once per 60 Hz
    // frame, so the raw (accumulated ticks) / (elapsed) instantaneous
    // velocity below stair-steps between frames; a short low-pass hides that
    // without adding perceptible lag to the scratch feel.
    private const double ScratchSmoothingTauSec = 0.02;
    // No tick for this long → the user let go of the platter.
    private static readonly TimeSpan ScratchReleaseIdle = TimeSpan.FromMilliseconds(80);
    // Release physics: CONSTANT deceleration (rate-units per second), like a real
    // platter under friction — not an exponential tau. This is what makes both
    // ends feel right: a hard backspin fling (|v| ~ 20) coasts for seconds and
    // covers bars before settling, while a small nudge (|v| ~ 1-2) recovers to
    // normal speed in ~0.1 s with no lingering slow-mo. Lower = longer coast.
    // Live-tune with SHOLTO_SCRATCH_DECEL.
    private static readonly double ScratchDecelPerSec =
        double.TryParse(Environment.GetEnvironmentVariable("SHOLTO_SCRATCH_DECEL"), out var i) && i > 0
            ? i : 12.0;
    // Below this speed-gap the coast switches from constant friction to an
    // exponential glide (CoastTailTauSec) — the drawn-out dying tail at the end
    // of a backspin, instead of stopping on a dime.
    private const double CoastKnee = 2.0;
    private const double CoastTailTauSec = 0.35;
    // Fling projection: the FLX4's platter has no flywheel — it stops almost the
    // instant the hand leaves — so a physical quarter-second rip reads as a weak
    // spin. When the platter is released ABOVE FlingThreshold (i.e. it was
    // genuinely flung, not slow-scrubbed), the release velocity is multiplied by
    // FlingBoost, projecting the momentum a weighted high-end platter would have
    // carried. Slow deliberate scrubs stay 1:1. Tune with SHOLTO_SCRATCH_FLING.
    // (The peak-hold launch already restores the rip's full speed, so this only
    // needs to add a little flywheel on top — 4.0 here was hilariously too much.)
    private static readonly double FlingBoost =
        double.TryParse(Environment.GetEnvironmentVariable("SHOLTO_SCRATCH_FLING"), out var f) && f >= 1
            ? f : 1.5;
    private const double FlingThreshold = 3.0;   // |rate| above this = a fling

    /// <summary>Per-deck: turn accumulated top-platter ticks into a smoothed
    /// signed varispeed rate and push it into the deck (ScratchRate), or —
    /// once ticks stop arriving — decay that rate back to the deck's resting
    /// rate and hand the provider back (EndScratch). No-op while the platter
    /// hasn't been touched (<see cref="ScratchState.Active"/> false).</summary>
    private void TickScratch(DeckViewModel deckVm, ScratchState st, DateTime now)
    {
        if (!st.Active) return;

        double dt = st.LastFlushAt == DateTime.MinValue ? 1.0 / 60.0 : (now - st.LastFlushAt).TotalSeconds;
        if (dt <= 0) dt = 1.0 / 60.0;
        st.LastFlushAt = now;

        bool tickedThisWindow = (now - st.LastTickAt) < ScratchReleaseIdle;

        if (tickedThisWindow)
        {
            // Grabbed and moving: exponential low-pass of the raw instantaneous
            // velocity (sum of this frame's tick deltas ÷ elapsed time) toward
            // the smoothed value Deck actually plays at.
            double rawVelocity = st.TickAccum / dt;
            st.TickAccum = 0;
            double alpha = 1 - Math.Exp(-dt / ScratchSmoothingTauSec);
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
            deckVm.Player.ScratchRate(st.Velocity);
            if (_scratchLog)
                Console.WriteLine($"[scratch] MOVE v={st.Velocity,7:F2} pos={deckVm.Player.PlayPosition,7:F3}");
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
            // project the momentum (see FlingBoost above) before friction takes it.
            if (!st.Coasting)
            {
                st.Coasting = true;
                // Launch the coast from the gesture's PEAK speed (see peak-hold
                // above), boosted — not from the smoothed velocity, which has
                // already decayed by the time the platter physically stopped.
                if (Math.Abs(st.PeakVelocity) >= FlingThreshold)
                {
                    st.Velocity = st.PeakVelocity * FlingBoost;
                    if (_scratchLog)
                        Console.WriteLine($"[scratch] FLING peak={st.PeakVelocity:F2} → v={st.Velocity:F2}");
                }
                st.PeakVelocity = 0;
            }
            double target = st.WasPlaying ? deckVm.Player.PlaybackSpeed : 0.0;
            double gap = Math.Abs(st.Velocity - target);
            if (gap > CoastKnee)
            {
                // Fast phase: constant friction, real momentum.
                double step = st.Decel * dt;
                if (st.Velocity < target) st.Velocity = Math.Min(target, st.Velocity + step);
                else                      st.Velocity = Math.Max(target, st.Velocity - step);
            }
            else
            {
                // Tail: below the knee, ease exponentially into rest — the last
                // stretch of a backspin draws out and dies away instead of
                // stopping on a dime.
                double a = 1 - Math.Exp(-dt / CoastTailTauSec);
                st.Velocity += (target - st.Velocity) * a;
            }

            if (Math.Abs(st.Velocity - target) < 0.02)
            {
                deckVm.Player.EndScratch();
                if (st.PauseAtEnd)
                {
                    // Vinyl brake finished: the platter has "stopped" — now pause.
                    deckVm.Player.Pause();
                    st.PauseAtEnd = false;
                }
                deckVm.IsScratching = false;
                st.Active = false;
                st.Coasting = false;
                st.LastFlushAt = DateTime.MinValue;
                // A scratch is NOT a jog: expire the jog-recency stamps so the
                // magnetic Quantize() (armed by "recently jogged, now idle")
                // doesn't fire ~180 ms after release and SeekRelative the deck
                // up to half a beat — the post-release hop to "a place the
                // timeline wasn't". The deck stays exactly where it coasted to.
                _vm.LastJogAt = DateTime.MinValue;
                if (ReferenceEquals(deckVm, _vm.Deck1)) _vm.LastJogAt1 = DateTime.MinValue;
                else                                    _vm.LastJogAt2 = DateTime.MinValue;
                if (_scratchLog)
                    Console.WriteLine($"[scratch] END  pos={deckVm.Player.PlayPosition,7:F3}");
            }
            else
            {
                if (_scratchLog)
                    Console.WriteLine($"[scratch] COAST v={st.Velocity,7:F2} pos={deckVm.Player.PlayPosition,7:F3}");
                deckVm.Player.ScratchRate(st.Velocity);
            }
        }
    }

    /// <summary>One deck's platter-scratch state — velocity accumulator,
    /// exponential smoother, and release-decay bookkeeping. See
    /// <see cref="TickScratch"/> for how it's driven and
    /// <see cref="HandleControllerEvent"/>'s JogRotated case for how ticks
    /// feed <see cref="TickAccum"/>.</summary>
    private sealed class ScratchState
    {
        /// <summary>True from the first top-platter tick until the release
        /// decay finishes (not just while ticks are actively arriving).</summary>
        public bool Active;
        /// <summary>Sum of (tick delta × secsPerTick) since the last Tick()
        /// flush — cleared every frame, same coalescing scheme as _pendingJog1/2.</summary>
        public double TickAccum;
        /// <summary>Smoothed signed track-seconds-per-second — what's actually
        /// pushed to Deck.ScratchRate.</summary>
        public double Velocity;
        /// <summary>Wall-clock of the most recent JogRotated tick.</summary>
        public DateTime LastTickAt = DateTime.MinValue;
        /// <summary>Wall-clock of the last Tick() flush, for the smoother's dt.</summary>
        public DateTime LastFlushAt = DateTime.MinValue;
        /// <summary>Whether the deck was Playing when the platter was grabbed —
        /// the release coast's target rate (forward speed vs stop).</summary>
        public bool WasPlaying;
        /// <summary>True once the hand has left and the coast is running — the
        /// fling boost fires exactly once, on the tick this flips true.</summary>
        public bool Coasting;
        /// <summary>Decaying peak of the gesture's smoothed velocity (~0.3 s
        /// memory) — the fling launches from this, since the platter has
        /// physically stopped (velocity ≈ 0) by the time release is detected.</summary>
        public double PeakVelocity;
        /// <summary>Deceleration for this coast (rate-units/s): the scratch
        /// friction constant normally, or the gentler vinyl-brake rate when
        /// <see cref="PauseAtEnd"/> is set.</summary>
        public double Decel = ScratchDecelPerSec;
        /// <summary>True while a vinyl-brake pause is in flight: when the coast
        /// reaches rest, the deck is paused instead of resuming.</summary>
        public bool PauseAtEnd;
    }

    public void Dispose()
    {
        _vm.ReanalyzeSelectedRequested -= OnReanalyzeSelectedRequested;
        _positionTimer?.Stop();
        _browseHoldTimer?.Stop();
    }
}
