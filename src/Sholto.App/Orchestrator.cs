using Microsoft.Extensions.Options;
using Avalonia.Threading;
using Microsoft.EntityFrameworkCore;
using Sholto.Analysis;
using Sholto.Audio;
using Sholto.Controller;
using Sholto.Controller.Gestures;
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
    // Every platter-feel number (seek step, scratch sensitivity, fling/coast/brake
    // physics) lives in ScratchOptions — tune there, not here.
    private readonly ScratchOptions _scratchOptions;
    // SHOLTO_SCRATCH_LOG=1 → print per-frame scratch velocity + play position, so
    // the platter's actual motion (and any position jumps) are visible in the log.
    private static readonly bool _scratchLog =
        Environment.GetEnvironmentVariable("SHOLTO_SCRATCH_LOG") == "1";
    // Per-deck platter-scratch state (top platter only — see ScratchState).
    private readonly ScratchState[] _scratch = { new(), new() };
    private readonly GestureRecognizer _recognizer;
    private DispatcherTimer? _positionTimer;

    public Orchestrator(MainViewModel vm, Func<IDbContextFactory<SholtoDbContext>?> dbFactory,
                        IOptions<ScratchOptions> scratch, GestureRecognizer recognizer)
    {
        _vm = vm;
        _dbFactory = dbFactory;
        _scratchOptions = scratch.Value;
        _recognizer = recognizer;
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
        st.Decel = 1.0 / _scratchOptions.BrakeSeconds;      // unity → 0 in ~BrakeSeconds
        st.TailTau = _scratchOptions.CoastTailTauSec;
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

    /// <summary>Raised when a deck's echo effect is toggled — App forwards this
    /// to the controller's PAD FX1 pad-1 LED. Args: deck, new on/off state.</summary>
    public event Action<int, bool>? EchoLightRequested;

    /// <summary>Repaint every LED this Orchestrator drives, from the app's own current
    /// state, without touching that state itself.
    /// <para>Why this exists: the Controller lights its own buttons on press — before
    /// it raises the event the App sees (see <c>ButtonWithLight.Press</c> /
    /// <c>Controller.OnMidi</c>) — so disabling the App's gesture table for Inspect mode
    /// cannot stop an LED changing. Press headphone CUE while the guide is open and the
    /// unit's LED flips while the App's real cue state never moves; the controller and
    /// the screen then disagree. <c>App</c> calls <see cref="Controller.Reset"/> (which
    /// blanks every LED, and forces the cue state back to off — see its own doc) and
    /// then this, every time gesture routing returns from Inspect to Play, so what's lit
    /// again matches what the app actually knows rather than whatever the hardware did
    /// while nobody who cared was listening.</para></summary>
    public void ReassertLights()
    {
        for (var deck = 0; deck < 2; deck++)
        {
            var deckVm = _vm.DeckFor(deck);
            BeatSyncLightRequested?.Invoke(deck, deckVm.BeatSyncLit);
            PadLightRequested?.Invoke(deck, 0, deckVm.DrumsActive);
            PadLightRequested?.Invoke(deck, 1, deckVm.VocalsActive);
            PadLightRequested?.Invoke(deck, 2, deckVm.InstrumentalActive);
            EchoLightRequested?.Invoke(deck, deckVm.EchoActive);
        }
    }

    /// <summary>Repairs a platter grab that Inspect mode stranded. While the guide is
    /// open the App's own gesture table is disabled (see <c>GestureRoutingChanged</c>
    /// in App.axaml.cs), so if a hand was already resting on a top platter when Inspect
    /// opened, the eventual <c>JogTouch(false)</c> lift never reaches
    /// <see cref="HandleJogTouch"/> — it's swallowed by the disabled table. That leaves
    /// <see cref="ScratchState.Touching"/> stuck true forever: <see cref="TickScratch"/>
    /// keeps treating the hand as "on" (<c>handOn = st.Touching || tickedThisWindow</c>)
    /// and writes <c>ScratchRate(0)</c> every frame, silencing that deck until the
    /// platter is touched and released again in Play mode. Called on the way back to
    /// Play, alongside <see cref="ReassertLights"/>, so a lift that never arrived is
    /// treated as if it just had — the normal release-decay path takes it from here.
    /// Deliberately does not touch anything else in ScratchState (velocity, decel,
    /// coasting): only the stuck touch flag is a bug, the rest is untouched physics.
    /// </summary>
    public void ReleaseStrandedScratchTouches()
    {
        foreach (var st in _scratch)
        {
            if (st.Active) st.Touching = false;
        }
    }

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

    /// <summary>Raised on every 16 ms tick, after the Orchestrator's own work.
    /// The App uses it to pump <see cref="GestureRecognizer.Tick"/> so the browse
    /// hold becomes due without a second timer.</summary>
    public event Action? Ticked;

    /// <summary>Start the 60 Hz tick (jog flush + magnetism + playhead sync).</summary>
    public void Start()
    {
        _positionTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(16) };
        _positionTimer.Tick += (_, _) => { Tick(); Ticked?.Invoke(); };
        _positionTimer.Start();
    }

    /// <summary>Act on one gesture. Called on the UI thread.
    /// <para>The gesture says what the DJ did. This method decides what Sholto does
    /// about it, which is where app state belongs: whether the deck can scratch,
    /// whether a loop is running, which deck a deckless gesture should hit.</para></summary>
    public void HandleGesture(Gesture g)
    {
        var vm = _vm;
        switch (g.Id)
        {
            case GestureIds.BrowseTurn:
                vm.OnBrowseRotated(((ControllerEvent.BrowseRotated)g.Source).Delta);
                break;

            case GestureIds.BrowsePressShort:
                // Deliberately nothing. LOAD 1 / LOAD 2 do the loading.
                break;

            case GestureIds.BrowsePressHold:
                ReanalyzeHighlighted("browse-hold");
                break;

            case GestureIds.LoadPress:
            {
                var sel = vm.SelectedTrack;
                if (sel is not null)
                {
                    var deck = vm.DeckFor(g.Deck);
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

            case GestureIds.PlayPress:
                vm.OnPlayPressed(g.Deck);
                break;

            case GestureIds.CrossfaderMove:
                vm.Crossfader = ((ControllerEvent.CrossfaderMoved)g.Source).Position;
                break;

            case GestureIds.VolumeMove:
                vm.DeckFor(g.Deck).ChannelGain = ((ControllerEvent.ChannelVolumeMoved)g.Source).Value;
                break;

            case GestureIds.CueHeadphoneToggle:
                vm.DeckFor(g.Deck).CueActive = ((ControllerEvent.CueChanged)g.Source).On;
                break;

            case GestureIds.MasterCueToggle:
                MasterCueRequested?.Invoke(((ControllerEvent.MasterCueChanged)g.Source).On);
                break;

            case GestureIds.EqTurn:
            {
                var e = (ControllerEvent.EqMoved)g.Source;
                vm.DeckFor(g.Deck).Player.SetEq((int)e.Band, e.Value);
                break;
            }

            case GestureIds.EqStemLevelTurn:
            {
                // HI → Drums, MID → Vocals, LOW → Instrumental, on either deck.
                var e = (ControllerEvent.EqMoved)g.Source;
                var deckVm = vm.DeckFor(g.Deck);
                switch (e.Band)
                {
                    case EqBand.High: deckVm.DrumsLevel        = e.Value; break;
                    case EqBand.Mid:  deckVm.VocalsLevel       = e.Value; break;
                    default:          deckVm.InstrumentalLevel = e.Value; break;
                }
                break;
            }

            case GestureIds.FilterTurn:
                vm.DeckFor(g.Deck).Player.SetFilter(((ControllerEvent.FilterMoved)g.Source).Position);
                break;

            case GestureIds.TempoMove:
                vm.DeckFor(g.Deck).SetTempoPosition(((ControllerEvent.TempoMoved)g.Source).Position);
                break;

            case GestureIds.PadStemDrums:
            case GestureIds.PadStemVocals:
            case GestureIds.PadStemInstrumental:
            {
                var st = (ControllerEvent.StemToggle)g.Source;
                var deckVm = vm.DeckFor(g.Deck);
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
                PadLightRequested?.Invoke(g.Deck, st.Group, nextActive);
                break;
            }

            case GestureIds.PadEcho:
            {
                var deckVm = vm.DeckFor(g.Deck);
                deckVm.EchoActive = !deckVm.EchoActive;
                EchoLightRequested?.Invoke(g.Deck, deckVm.EchoActive);
                break;
            }

            case GestureIds.BeatLoopToggle:
                vm.DeckFor(g.Deck).Player.EnableBeatLoop(((ControllerEvent.BeatLoopToggle)g.Source).Bars);
                break;
            case GestureIds.BeatLoopHalve:
                vm.DeckFor(g.Deck).Player.HalveLoop();
                break;
            case GestureIds.BeatLoopDouble:
                vm.DeckFor(g.Deck).Player.DoubleLoop();
                break;

            case GestureIds.SyncPress:
                // Beat sync is not implemented yet.
                break;

            case GestureIds.CueTransportPlain:
                // Deliberately nothing. See DdjFlx4Mapping's note on why the old
                // beatgrid re-anchor binding was removed.
                break;

            case GestureIds.CueTransportRestart:
                vm.DeckFor(g.Deck).Player.SeekToFraction(0);
                break;

            case GestureIds.SyncCyclePitchRange:
                vm.DeckFor(g.Deck).CyclePitchRange();
                break;

            case GestureIds.GridNudgeBack:
            case GestureIds.GridNudgeForward:
            {
                int beats = ((ControllerEvent.NudgeGrid)g.Source).Beats;
                if (g.Deck >= 0) { vm.DeckFor(g.Deck).Player.NudgeGrid(beats); break; }
                // The BEAT arrows are one pair shared by both decks, so they arrive
                // deckless. Pick a deck: held Shift first, then whichever deck has a
                // loop running, then deck 0.
                int target;
                if (_recognizer.IsShiftHeld(0)) target = 0;
                else if (_recognizer.IsShiftHeld(1)) target = 1;
                else if (vm.DeckFor(0).Player.ActiveLoop is not null) target = 0;
                else if (vm.DeckFor(1).Player.ActiveLoop is not null) target = 1;
                else target = 0;
                vm.DeckFor(target).Player.NudgeGrid(beats);
                break;
            }

            case GestureIds.ShiftHold:
            case GestureIds.StemLevelHold:
            case GestureIds.PadModeHotCue:
            case GestureIds.PadModePadFx1:
                // State only. The recognizer holds the modifier state; the Controller
                // already repaints the pad LEDs on a page switch.
                break;

            case GestureIds.JogTopTouch:
                HandleJogTouch((ControllerEvent.JogTouch)g.Source);
                break;

            case GestureIds.JogTopShiftTurn:
            {
                // Silent 2x seek through the track, bypassing the audible scratch.
                // (The "4x" in DdjFlx4Mapping's comment is stale — see the living doc.)
                var j = (ControllerEvent.JogRotated)g.Source;
                var deckVm = vm.DeckFor(g.Deck);
                if (deckVm.Player.ActiveLoop is not null) break;
                double fastSecs = j.Delta * _scratchOptions.TopPlatterSecsPerTick * 2;
                if (g.Deck == 0) _pendingJog1 += fastSecs; else _pendingJog2 += fastSecs;
                MarkJogged(g.Deck);
                break;
            }

            case GestureIds.JogTopTurn:
            case GestureIds.JogRingTurn:
                HandleJogTurn(g);
                break;

            default:
                break;   // an id nothing acts on yet
        }
    }

    private void MarkJogged(int deck)
    {
        _vm.LastJoggedDeck = deck == 0 ? 1 : 2;
        var nowUtc = DateTime.UtcNow;
        _vm.LastJogAt = nowUtc;
        if (deck == 0) _vm.LastJogAt1 = nowUtc; else _vm.LastJogAt2 = nowUtc;
    }

    private void HandleJogTouch(ControllerEvent.JogTouch jt)
    {
        var vm = _vm;
        var deckVm = vm.DeckFor(jt.Deck);
        var st = _scratch[jt.Deck];
        st.Touching = jt.Touching;
        // Hand lands: grab now, at rate = the deck's current speed, so
        // the sound holds under the finger (rate → 0 as no ticks
        // arrive) rather than waiting for the first tick. Shift + touch
        // is the silent fast-search, not a grab; a brake in flight
        // keeps its own state.
        if (jt.Touching && !st.Active && !_recognizer.IsShiftHeld(jt.Deck) && deckVm.Player.CanScratch)
        {
            st.Active = true;
            st.PauseAtEnd = false;
            st.Decel = _scratchOptions.DecelPerSec;
            st.TailTau = _scratchOptions.CoastTailTauSec;
            st.WasPlaying = deckVm.Player.IsPlaying;
            st.Velocity = st.WasPlaying ? deckVm.Player.PlaybackSpeed : 0;
            st.PeakVelocity = 0;
            deckVm.IsScratching = true;
            if (_scratchLog)
                Console.WriteLine($"[scratch] TOUCH deck={jt.Deck} playing={st.WasPlaying} pos={deckVm.Player.PlayPosition:F3}");
        }
        else if (!jt.Touching && _scratchLog)
            Console.WriteLine($"[scratch] LIFT  deck={jt.Deck} v={st.Velocity:F2}");
    }

    private void HandleJogTurn(Gesture g)
    {
        var vm = _vm;
        var j = (ControllerEvent.JogRotated)g.Source;
        // Loop locked: the jog wheel is ignored while a loop is active, else
        // scrubbing could pull the playhead outside the loop and break the wrap.
        var deckVm = vm.DeckFor(j.Deck);
        if (deckVm.Player.ActiveLoop is not null) return;

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
                st.Decel = _scratchOptions.DecelPerSec;
            st.TailTau = _scratchOptions.CoastTailTauSec;
                st.WasPlaying = deckVm.Player.IsPlaying;
                // Start from the deck's actual current rate, not 0 — a
                // grab on a playing deck shouldn't hiccup to silence
                // before the hand's motion takes over.
                st.Velocity = st.WasPlaying ? deckVm.Player.PlaybackSpeed : 0;
                deckVm.IsScratching = true;
                if (_scratchLog)
                    Console.WriteLine($"[scratch] GRAB deck={j.Deck} playing={st.WasPlaying} pos={deckVm.Player.PlayPosition:F3}");
            }
            st.TickAccum += j.Delta * _scratchOptions.ScratchSecsPerTick;
            st.LastTickAt = DateTime.UtcNow;
        }
        else
        {
            // Accumulate; Tick() flushes it into one Seek per frame — each Seek
            // flushes SoundFlow's buffer, so per-event seeks (~100/s) would glitch.
            double secsPerTick = j.Source == JogSource.TopPlatter ? _scratchOptions.TopPlatterSecsPerTick : _scratchOptions.SideRingSecsPerTick;
            if (j.Deck == 0) _pendingJog1 += j.Delta * secsPerTick;
            else             _pendingJog2 += j.Delta * secsPerTick;
        }
        MarkJogged(j.Deck);
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

        // Hand on = the touch sensor says so, OR ticks are still arriving (a
        // flung platter keeps ticking after the hand has lifted — that free
        // spin is the real backspin, so it counts as "still driving"). The
        // tick-gap timeout is only a fallback now, not the release detector.
        bool tickedThisWindow = (now - st.LastTickAt).TotalMilliseconds < _scratchOptions.ReleaseIdleMs;
        bool handOn = st.Touching || tickedThisWindow;

        if (handOn)
        {
            // Grabbed and moving: exponential low-pass of the raw instantaneous
            // velocity (sum of this frame's tick deltas ÷ elapsed time) toward
            // the smoothed value Deck actually plays at.
            double rawVelocity = st.TickAccum / dt;
            st.TickAccum = 0;
            double alpha = 1 - Math.Exp(-dt / _scratchOptions.SmoothingTauSec);
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
            double target = st.WasPlaying ? deckVm.Player.PlaybackSpeed : 0.0;
            if (!st.Coasting)
            {
                st.Coasting = true;
                if (Math.Abs(st.PeakVelocity) >= _scratchOptions.FlingThreshold)
                {
                    // A genuine fling: launch the coast from the gesture's PEAK
                    // speed (see peak-hold above), boosted — not from the
                    // smoothed velocity, which has already decayed by the time
                    // the platter physically stopped.
                    st.Velocity = st.PeakVelocity * _scratchOptions.FlingBoost;
                    // A spinback dies out faster than a forward fling: more
                    // friction and a shorter tail, so it completes in 1/N the time.
                    if (st.Velocity < 0)
                    {
                        st.Decel *= _scratchOptions.SpinbackSpeedup;
                        st.TailTau = _scratchOptions.CoastTailTauSec / _scratchOptions.SpinbackSpeedup;
                    }
                    if (_scratchLog)
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
            if (gap > _scratchOptions.CoastKnee)
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
    /// <see cref="HandleJogTurn"/>'s JogRotated case for how ticks
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
        /// <summary>True while the touch sensor reports a hand on the platter
        /// top. Authoritative grab/hold; release = hand off AND ticks stopped.</summary>
        public bool Touching;
        /// <summary>Decaying peak of the gesture's smoothed velocity (~0.3 s
        /// memory) — the fling launches from this, since the platter has
        /// physically stopped (velocity ≈ 0) by the time release is detected.</summary>
        public double PeakVelocity;
        /// <summary>Deceleration for this coast (rate-units/s): the scratch
        /// friction constant normally, or the gentler vinyl-brake rate when
        /// <see cref="PauseAtEnd"/> is set.</summary>
        public double Decel;   // set at GRAB (DecelPerSec) or brake (1/BrakeSeconds)
        /// <summary>Time constant of the dying tail for this coast — the option
        /// default, shortened for a spinback (see SpinbackSpeedup).</summary>
        public double TailTau;
        /// <summary>True while a vinyl-brake pause is in flight: when the coast
        /// reaches rest, the deck is paused instead of resuming.</summary>
        public bool PauseAtEnd;
    }

    public void Dispose()
    {
        _vm.ReanalyzeSelectedRequested -= OnReanalyzeSelectedRequested;
        _positionTimer?.Stop();
    }
}
