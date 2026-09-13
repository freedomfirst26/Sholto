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
    private readonly IControlSurface _surface;
    private readonly IKeyboard _keyboard;
    private readonly IApplication _app;
    private readonly Func<IDbContextFactory<SholtoDbContext>?> _dbFactory;
    private readonly IAudioFileDecoder _decoder;
    // Built lazily from _dbFactory the first time a reanalyze needs to save a key
    // (the DB may legitimately not exist yet when Orchestrator is constructed —
    // see _dbFactory's own doc), then reused — not rebuilt per invocation.
    private SqliteKeyAnalysisStore? _keyAnalysisCache;

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
    private readonly IGestureRecognizer _recognizer;
    private DispatcherTimer? _positionTimer;

    // Orchestrator's own working state for the jog wheel — see JogTracker's doc
    // for why this isn't a ViewModel property any more.
    private readonly JogTracker _jog = new();

    // Magnetic beat-snap: tuning + state. Moved here from MainViewModel along with
    // the jog state it's computed from — see the split plan in ~/Projects/sholto.md.
    private readonly MagnetismOptions _magnetismOptions;
    private bool _lastMagnetEligible;
    private bool _quantizeFired;
    private const double EngageThreshold = 0.3;     // same as glow threshold — see one, fire one
    private const double DisengageThreshold = 0.15; // hysteresis to avoid re-fire chatter
    private static readonly TimeSpan JogIdleForQuantize = TimeSpan.FromMilliseconds(180);
    // Auto-quantize only counts as "user released a jog gesture" if the jog was
    // recent. Without this window, two decks running at different tempos would
    // eventually drift into alignment and an old jog from minutes ago would
    // trigger a surprise seek.
    private static readonly TimeSpan JogRecencyForQuantize = TimeSpan.FromSeconds(2);

    /// <summary>The three entities Orchestrator glues together, per the
    /// three-entity refactor recorded in ~/Projects/sholto.md: a control surface
    /// (real FLX4 <c>Controller</c> or Bench's <c>ScriptedControlSurface</c>), a
    /// keyboard (real <c>MainWindow</c> or a scripted one), and the app itself
    /// (composition of the four real roles — see <see cref="IApplication"/>).
    /// Orchestrator owns the LED relay for <paramref name="surface"/> directly
    /// (<see cref="ReassertLights"/> and the deck-light forwarding below call it),
    /// and turns every keyboard gesture that has a control-surface equivalent
    /// into the SAME app call the equivalent gesture makes (see
    /// <see cref="OnKeyboardAction"/> and <see cref="HandleGesture"/>'s
    /// <c>LoadPress</c>/<c>PlayPress</c> cases).</summary>
    public Orchestrator(IControlSurface surface, IKeyboard keyboard, IApplication app,
                        Func<IDbContextFactory<SholtoDbContext>?> dbFactory,
                        IOptions<ScratchOptions> scratch, IOptions<MagnetismOptions> magnetism,
                        IGestureRecognizer recognizer, IAudioFileDecoder decoder)
    {
        _surface = surface;
        _keyboard = keyboard;
        _app = app;
        _dbFactory = dbFactory;
        _decoder = decoder;
        _scratchOptions = scratch.Value;
        _magnetismOptions = magnetism.Value;
        _recognizer = recognizer;
        // Double-clicking a library row re-analyzes it — same path as the browse
        // long-press. The VM only raises the request; we hold the provider + factory.
        _app.ReanalyzeSelectedRequested += OnReanalyzeSelectedRequested;

        // Deck transport → controller output: each deck raises the resolved LED state
        // (solid while Playing, blinking while Ending, off while Stopped — the deck
        // owns the flash clock so the LED stays in lockstep with its disc ring). We
        // drive that deck's BEAT SYNC LED directly — Orchestrator owns the LED
        // relay now, App no longer forwards it.
        _app.Deck1.DeckLightChanged += on => _surface.SetBeatSync(0, on);
        _app.Deck2.DeckLightChanged += on => _surface.SetBeatSync(1, on);

        // Pause on a scratch-capable deck = vinyl brake: ride the scratch coast
        // down to zero (pitch falling like a stopping turntable), THEN pause —
        // instead of cutting to stone silence. Second press mid-brake cancels
        // and spins back to normal playback.
        _app.BrakePauseRequested += OnBrakePauseRequested;

        // Keyboard → Orchestrator → app: the other half of the fix. A keyboard
        // gesture that has a control-surface equivalent (see IKeyboard's doc)
        // reaches the exact same app call the equivalent controller gesture does.
        _keyboard.Action += OnKeyboardAction;
    }

    /// <summary>Turn one keyboard gesture into the same app call its control-surface
    /// equivalent makes. <see cref="KeyboardGesture.LoadSelected"/> shares
    /// <see cref="LoadSelectedIntoDeck"/> with <c>GestureIds.LoadPress</c>;
    /// <see cref="KeyboardGesture.Play"/> shares <see cref="PlayPressed"/> with
    /// <c>GestureIds.PlayPress</c>. <see cref="KeyboardGesture.AddMarker"/> and
    /// <see cref="KeyboardGesture.OpenGridEdit"/> have no FLX4 equivalent (see
    /// IKeyboard's doc) but are handled here too, per the keyboard-split decision.</summary>
    private void OnKeyboardAction(KeyboardEvent k)
    {
        switch (k.Kind)
        {
            case KeyboardGesture.LoadSelected:
                LoadSelectedIntoDeck(k.Deck);
                break;
            case KeyboardGesture.Play:
                PlayPressed(k.Deck);
                break;
            case KeyboardGesture.AddMarker:
                _ = _app.AddMarkerToTargetDeckAsync(k.Deck);
                break;
            case KeyboardGesture.OpenGridEdit:
                GridEditTarget()?.OpenEdit();
                break;
        }
    }

    /// <summary>Which deck a grid edit applies to: the deck whose grid editor is
    /// already open, else the one with an active loop, else the first loaded deck,
    /// else null. Mirrors <c>MainWindow.GridTarget</c> — that copy stays in the view
    /// for the phase/BPM-tune keys (←/→/↑/↓), which remain UI chrome and are out of
    /// scope for this port; duplicated rather than shared to avoid widening either
    /// side's surface for one four-line targeting rule.</summary>
    private DeckViewModel? GridEditTarget()
    {
        if (_app.Deck1.EditOpen) return _app.Deck1;
        if (_app.Deck2.EditOpen) return _app.Deck2;
        if (_app.Deck1.Player.ActiveLoop is not null) return _app.Deck1;
        if (_app.Deck2.Player.ActiveLoop is not null) return _app.Deck2;
        if (_app.Deck1.Player.IsLoaded) return _app.Deck1;
        if (_app.Deck2.Player.IsLoaded) return _app.Deck2;
        return null;
    }

    /// <summary>Raised whenever magnet-lock eligibility flips. The App composition
    /// root wires this to <c>MainViewModel.IsMagnetEligible</c> so the centerline
    /// magnet glyph still updates, without Orchestrator holding a concrete
    /// MainViewModel reference — the same "raise an event, App forwards it"
    /// pattern <see cref="MasterCueRequested"/> uses.</summary>
    public event Action<bool>? MagnetEligibilityChanged;

    private void OnBrakePauseRequested(int deck)
    {
        var st = _scratch[deck];
        var deckVm = _app.DeckFor(deck);

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

    /// <summary>Raised when MASTER CUE is toggled — App forwards it to the audio
    /// engine's master-cue monitor. Bool is the new on/off state. Unlike the other
    /// three light requests (BEAT SYNC / pad / echo), this one's destination is the
    /// audio engine, not the control surface, so it stays an event App relays rather
    /// than a call Orchestrator makes directly — Orchestrator has no reference to
    /// the audio engine and shouldn't gain one just for this.</summary>
    public event Action<bool>? MasterCueRequested;

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
    /// while nobody who cared was listening.</para>
    /// <para>Calls <see cref="_surface"/> directly — Orchestrator owns the LED relay
    /// now, App no longer forwards <c>BeatSyncLightRequested</c>/<c>PadLightRequested</c>/
    /// <c>EchoLightRequested</c> events (removed; this is the only caller they had).</para></summary>
    public void ReassertLights()
    {
        for (var deck = 0; deck < 2; deck++)
        {
            var deckVm = _app.DeckFor(deck);
            _surface.SetBeatSync(deck, deckVm.BeatSyncLit);
            _surface.SetPadLight(deck, 0, deckVm.DrumsActive);
            _surface.SetPadLight(deck, 1, deckVm.VocalsActive);
            _surface.SetPadLight(deck, 2, deckVm.InstrumentalActive);
            _surface.SetEchoLight(deck, deckVm.EchoActive);
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
        var provider = _app.Deck1.Player.AnalysisProvider;
        if (provider is null) { Console.WriteLine($"[Orchestrator] {source} re-analyze: no AnalysisProvider yet"); return; }
        Console.WriteLine($"[Orchestrator] {source} → re-analyzing {_app.SelectedTrack?.FilePath}");
        var keyCache = KeyAnalysisCacheOrNull();
        _ = _app.OnBrowseHeldAsync(
            t => _decoder.Decode(t.FilePath),
            provider,
            saveKey: keyCache is not null ? keyCache.PutAsync : null);
    }

    /// <summary>Resolves once the DB factory becomes available, then reused — a
    /// cache constructed per invocation was the flagged bug (see the architectural
    /// review's Pass 1 addendum); a truly eager, constructor-injected instance isn't
    /// possible here because <see cref="_dbFactory"/> is deliberately lazy (the DB
    /// may not exist yet at construction time).</summary>
    private SqliteKeyAnalysisStore? KeyAnalysisCacheOrNull()
    {
        if (_keyAnalysisCache is not null) return _keyAnalysisCache;
        var factory = _dbFactory();
        if (factory is null) return null;
        return _keyAnalysisCache = new SqliteKeyAnalysisStore(factory);
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

    /// <summary>Load the highlighted library track into a deck. Shared by
    /// <c>GestureIds.LoadPress</c> (FLX4 LOAD 1/2) and
    /// <see cref="KeyboardGesture.LoadSelected"/> (keyboard 1/2) — the same code
    /// either path runs, not just the same outcome.</summary>
    private void LoadSelectedIntoDeck(int deckIndex)
    {
        var sel = _app.SelectedTrack;
        if (sel is null) return;
        var deck = _app.DeckFor(deckIndex);
        var mult = _app.GetBpmMultiplierFor(sel.FilePath);
        deck.BeginLoad(sel, mult);
        _ = Task.Run(async () =>
        {
            var samples = _decoder.Decode(sel.FilePath);
            await Dispatcher.UIThread.InvokeAsync(() =>
                deck.LoadTrack(sel, sel.FilePath, samples, mult));
        });
    }

    /// <summary>Play/pause a deck. Shared by <c>GestureIds.PlayPress</c> (FLX4 PLAY)
    /// and <see cref="KeyboardGesture.Play"/> (keyboard P) — same call, same path.</summary>
    private void PlayPressed(int deckIndex) => _app.OnPlayPressed(deckIndex);

    /// <summary>Build the full gesture→action table for App's "app" GestureBindings,
    /// replacing what used to be a 34-arm switch in <c>HandleGesture</c> (removed —
    /// see <c>~/Projects/sholto.md</c> for the dissolution plan). Delegates to five
    /// small classes grouped by what they actually touch (transport, mixer, pads,
    /// loops, browse); each is independently testable with a fake deck. The four
    /// jog/scratch ids stay wired directly to Orchestrator's own methods below —
    /// the scratch engine is deliberately not touched or extracted in this pass, so
    /// its call sites are left exactly where they were, just addressed by id
    /// instead of by switch arm.
    /// <para>Every gesture id in <see cref="GestureIds.All"/> must appear here
    /// exactly once — a test asserts the two sets match.</para></summary>
    public Dictionary<string, Action<Gesture>> BuildGestureTable()
    {
        var map = new Dictionary<string, Action<Gesture>>();

        TransportBindings.Add(map, _app, PlayPressed, on => MasterCueRequested?.Invoke(on));
        MixerBindings.Add(map, _app);
        PadBindings.Add(map, _app, _surface);
        LoopBindings.Add(map, _app, _recognizer);
        BrowseBindings.Add(map, _app, LoadSelectedIntoDeck, ReanalyzeHighlighted);

        // Jog/scratch — left as direct Orchestrator calls; see this method's doc.
        map[GestureIds.JogTopTouch] = g => HandleJogTouch((ControllerEvent.JogTouch)g.Source);
        map[GestureIds.JogTopShiftTurn] = HandleJogTopShiftTurn;
        map[GestureIds.JogTopTurn] = HandleJogTurn;
        map[GestureIds.JogRingTurn] = HandleJogTurn;

        return map;
    }

    /// <summary>Silent 2x seek through the track, bypassing the audible scratch.
    /// (The "4x" in DdjFlx4Mapping's comment is stale — see the living doc.)</summary>
    private void HandleJogTopShiftTurn(Gesture g)
    {
        var j = (ControllerEvent.JogRotated)g.Source;
        var deckVm = _app.DeckFor(g.Deck);
        if (deckVm.Player.ActiveLoop is not null) return;
        double fastSecs = j.Delta * _scratchOptions.TopPlatterSecsPerTick * 2;
        if (g.Deck == 0) _pendingJog1 += fastSecs; else _pendingJog2 += fastSecs;
        MarkJogged(g.Deck);
    }

    private void MarkJogged(int deck) => _jog.MarkJogged(deck);

    private void HandleJogTouch(ControllerEvent.JogTouch jt)
    {
        var deckVm = _app.DeckFor(jt.Deck);
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
        var j = (ControllerEvent.JogRotated)g.Source;
        // Loop locked: the jog wheel is ignored while a loop is active, else
        // scrubbing could pull the playhead outside the loop and break the wrap.
        var deckVm = _app.DeckFor(j.Deck);
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
        double scale = 1 - MagnetismFactor() * 0.9;
        if (_pendingJog1 != 0) { _app.Deck1.Player.SeekRelative(_pendingJog1 * scale); _pendingJog1 = 0; }
        if (_pendingJog2 != 0) { _app.Deck2.Player.SeekRelative(_pendingJog2 * scale); _pendingJog2 = 0; }

        var now = DateTime.UtcNow;
        _app.Deck1.IsScrubbing = _jog.LastJoggedDeck == 1 && (now - _jog.LastJogAt) < JogTracker.ActiveJogWindow;
        _app.Deck2.IsScrubbing = _jog.LastJoggedDeck == 2 && (now - _jog.LastJogAt) < JogTracker.ActiveJogWindow;

        TickScratch(_app.Deck1, _scratch[0], now);
        TickScratch(_app.Deck2, _scratch[1], now);

        UpdateMagnetism();

        if (_app.Deck1.Player.IsLoaded) _app.Deck1.SyncPlayPosition();
        if (_app.Deck2.Player.IsLoaded) _app.Deck2.SyncPlayPosition();
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
                _jog.ClearAfterScratchEnd(ReferenceEquals(deckVm, _app.Deck1));
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

    /// <summary>
    /// Magnet-lock eligibility. True iff:
    /// <list type="bullet">
    ///   <item>both decks have completed basic analysis (BPM + beat grid),</item>
    ///   <item>both decks are actually playing,</item>
    ///   <item>their <em>playback</em> BPMs (source × multiplier × tempo fader)
    ///         are within <see cref="MagnetismOptions.BpmEligibilityTolerance"/>,</item>
    ///   <item>the user isn't currently rotating <em>both</em> jog wheels at
    ///         once (a dual-jog gesture is the user doing something deliberate;
    ///         the magnet should hold off until they release one).</item>
    /// </list>
    /// Moved here from <c>MainViewModel</c> — see the split plan in
    /// <c>~/Projects/sholto.md</c> ("MagnetismFactor/UpdateMagnetism ... follow
    /// the same path [as the jog state], since magnetism is computed from jog
    /// recency").</summary>
    private bool IsBpmEligibleForMagnetism()
    {
        var deck1 = _app.Deck1;
        var deck2 = _app.Deck2;
        if (!deck1.HasAnalysis || !deck2.HasAnalysis) return false;
        if (!deck1.Player.IsPlaying || !deck2.Player.IsPlaying) return false;
        // A scratching deck isn't a candidate for a magnetic beat-snap —
        // Quantize()'s SeekRelative would yank the platter out from under
        // the user's hand mid-gesture. (Also covers the force-Play() a
        // paused deck gets while scratched: without this gate that alone
        // could newly satisfy "both decks playing" and fire a surprise snap.)
        if (deck1.IsScratching || deck2.IsScratching) return false;

        double eff1 = deck1.EffectiveBpm;
        double eff2 = deck2.EffectiveBpm;
        if (eff1 <= 0 || eff2 <= 0) return false;

        double diff = Math.Abs(eff1 - eff2) / Math.Max(eff1, eff2);
        if (diff > _magnetismOptions.BpmEligibilityTolerance) return false;

        // Both decks being jogged simultaneously → user is in the middle of
        // a manual adjustment, don't surprise them with a lock.
        if (_jog.BothDecksActivelyJogging) return false;

        return true;
    }

    /// <summary>
    /// 0..1: 1 when both decks are playing and their nearest beats are in-phase,
    /// 0 when out of the magnetic window. Returns 0 unconditionally when BPMs
    /// aren't eligible — without this gate, two decks running far apart in tempo
    /// would still drift into phase alignment every few bars and trigger a
    /// surprise snap.
    /// </summary>
    private double MagnetismFactor()
    {
        if (!IsBpmEligibleForMagnetism()) return 0;
        var deck1 = _app.Deck1;
        var deck2 = _app.Deck2;
        var d1 = deck1.Analysis.Basic?.DownbeatTimes;
        var d2 = deck2.Analysis.Basic?.DownbeatTimes;
        if (d1 is null || d1.Length == 0 || d2 is null || d2.Length == 0) return 0;

        double phase1 = deck1.PlaybackSeconds - deck1.NearestDownbeatSec();
        double phase2 = deck2.PlaybackSeconds - deck2.NearestDownbeatSec();
        double misalign = Math.Abs(phase1 - phase2);

        const double window = 0.15;  // 150 ms — bar-start tolerance is wider than beat-start
        double t = Math.Min(misalign / window, 1);
        return 1 - t * t * (3 - 2 * t);  // smoothstep, 1 at t=0 → 0 at t=1
    }

    /// <summary>Push current magnetism state into each deck's MagneticGlowSec for the UI.
    /// Also runs the engaged → release → quantize state machine.</summary>
    private void UpdateMagnetism()
    {
        // Publish the binary eligibility so the centerline magnet glyph
        // pops in/out via its own style-class transition (App wires this to
        // MainViewModel.IsMagnetEligible — see MagnetEligibilityChanged).
        bool eligible = IsBpmEligibleForMagnetism();
        if (eligible != _lastMagnetEligible)
        {
            _lastMagnetEligible = eligible;
            MagnetEligibilityChanged?.Invoke(eligible);
        }

        double f = MagnetismFactor();

        if (f < DisengageThreshold)
            _quantizeFired = false;  // user pulled them apart; re-arm

        // Fire once: greens visible + user let go of the jog for a beat.
        // Crucial gate: ignore if the user hasn't jogged at all this session
        // (LastJogAt = DateTime.MinValue), or if their last jog was so long ago
        // that "the user just let go" isn't a believable framing any more. This
        // is what stops two decks running at different tempos from triggering a
        // surprise seek every time their phases drift into alignment.
        var sinceJog = DateTime.UtcNow - _jog.LastJogAt;
        bool userRecentlyReleasedJog =
            _jog.LastJogAt != DateTime.MinValue
            && sinceJog > JogIdleForQuantize
            && sinceJog < JogRecencyForQuantize;

        if (!_quantizeFired
            && f >= EngageThreshold
            && userRecentlyReleasedJog)
        {
            Quantize();
            _quantizeFired = true;
        }

        // Show greens whenever engaged AND we haven't snapped yet. Once snapped, the
        // visuals collapse — that's the "locked, hands off" signal.
        bool active = f >= EngageThreshold && !_quantizeFired;
        _app.Deck1.MagneticGlowSec = active ? _app.Deck1.NearestDownbeatSec() : -1;
        _app.Deck2.MagneticGlowSec = active ? _app.Deck2.NearestDownbeatSec() : -1;
    }

    /// <summary>Snap the last-jogged deck to the reference deck — phase aligns
    /// the downbeats AND tempo-locks so the link actually holds. Without the
    /// tempo lock, a fraction-of-a-percent BPM difference (e.g. 176.5 vs 176.6)
    /// would let the decks drift apart immediately after the snap.</summary>
    private void Quantize()
    {
        var deck1 = _app.Deck1;
        var deck2 = _app.Deck2;
        DeckViewModel adjusted, reference;
        if (_jog.LastJoggedDeck == 1 || _jog.LastJoggedDeck == 2)
        {
            adjusted  = _jog.LastJoggedDeck == 1 ? deck1 : deck2;
            reference = _jog.LastJoggedDeck == 1 ? deck2 : deck1;
        }
        else
        {
            // No jog history — pick whichever deck is further from its own downbeat
            // (the one with more error to correct).
            double e1 = Math.Abs(deck1.PlaybackSeconds - deck1.NearestDownbeatSec());
            double e2 = Math.Abs(deck2.PlaybackSeconds - deck2.NearestDownbeatSec());
            (adjusted, reference) = e1 > e2 ? (deck1, deck2) : (deck2, deck1);
        }

        // 1) Tempo-lock: pull the adjusted deck's EffectiveBpm onto the reference.
        //    Done first so the phase math below works against the locked tempo.
        adjusted.MatchEffectiveBpm(reference.EffectiveBpm);

        // 2) Phase-snap: shift adjusted so its next downbeat lands at the same
        //    wall-clock moment as the reference's next downbeat. Math: place
        //    adj at (its nearest downbeat) + (ref's offset past *its* nearest
        //    downbeat). Walks the same number of seconds past a downbeat as
        //    ref, so the next beats fire together — independent of which bar
        //    of either song they happen to be in.
        double refPhase = reference.PlaybackSeconds - reference.NearestDownbeatSec();
        double adjDownbeat = adjusted.NearestDownbeatSec();
        if (adjDownbeat < 0) return;
        double delta = (adjDownbeat + refPhase) - adjusted.PlaybackSeconds;
        if (Math.Abs(delta) > 0.0001) adjusted.Player.SeekRelative(delta);
        Console.WriteLine($"[Magnet] snap: refPhase={refPhase:F4}s adjDownbeat={adjDownbeat:F4}s delta={delta:F4}s | refBpm={reference.EffectiveBpm:F3} adjBpm={adjusted.EffectiveBpm:F3}");
    }

    /// <summary>Orchestrator's own working state for the jog wheel — when each deck
    /// was last jogged, and which deck was jogged most recently. Previously these
    /// were settable properties on <c>MainViewModel</c> (<c>IDeckHost</c>):
    /// Orchestrator wrote its own working state into the ViewModel and read it back,
    /// a shared mutable scratchpad rather than a real collaboration. Read every use
    /// in Orchestrator.cs and MainViewModel.cs before moving this (see the split
    /// plan in <c>~/Projects/sholto.md</c>) — MainViewModel's own magnetism
    /// calculation read these too, which is why <see cref="IsBpmEligibleForMagnetism"/>
    /// / <see cref="MagnetismFactor"/> / <see cref="UpdateMagnetism"/> /
    /// <see cref="Quantize"/> moved here alongside the jog state itself, rather than
    /// leaving MainViewModel to somehow read Orchestrator's private state.</summary>
    private sealed class JogTracker
    {
        /// <summary>Deck most recently nudged by the jog wheel (1 or 2), or -1 if
        /// neither has been jogged yet. The other deck acts as Quantize's reference.</summary>
        public int LastJoggedDeck { get; private set; } = -1;
        /// <summary>Wall-clock of last jog tick — used to detect "user let go" for quantize.</summary>
        public DateTime LastJogAt { get; private set; } = DateTime.MinValue;
        /// <summary>Wall-clock of the last jog event on deck 1 specifically. Used so
        /// we can tell "both decks are being touched right now" apart from "one is".</summary>
        public DateTime LastJogAt1 { get; private set; } = DateTime.MinValue;
        /// <summary>Wall-clock of the last jog event on deck 2 specifically.</summary>
        public DateTime LastJogAt2 { get; private set; } = DateTime.MinValue;

        // How recently a jog event has to have arrived for that deck to count as
        // "actively being adjusted right now". Also used directly by
        // Orchestrator.Tick() for the IsScrubbing window, so the two always agree.
        internal static readonly TimeSpan ActiveJogWindow = TimeSpan.FromMilliseconds(250);

        public bool BothDecksActivelyJogging =>
            IsActivelyJogging(LastJogAt1) && IsActivelyJogging(LastJogAt2);

        private static bool IsActivelyJogging(DateTime deckLastJog) =>
            deckLastJog != DateTime.MinValue
            && DateTime.UtcNow - deckLastJog < ActiveJogWindow;

        /// <param name="deck">Zero-based deck index (0 or 1), as gestures carry it.</param>
        public void MarkJogged(int deck)
        {
            LastJoggedDeck = deck == 0 ? 1 : 2;
            var nowUtc = DateTime.UtcNow;
            LastJogAt = nowUtc;
            if (deck == 0) LastJogAt1 = nowUtc; else LastJogAt2 = nowUtc;
        }

        /// <summary>A scratch ending is NOT a jog release: expire the recency stamps
        /// so the magnetic Quantize() (armed by "recently jogged, now idle") doesn't
        /// misfire off the coast-to-rest that follows a scratch. See TickScratch's
        /// own comment at the call site.</summary>
        /// <param name="isDeck1">Which deck the scratch that just ended was on.</param>
        public void ClearAfterScratchEnd(bool isDeck1)
        {
            LastJogAt = DateTime.MinValue;
            if (isDeck1) LastJogAt1 = DateTime.MinValue; else LastJogAt2 = DateTime.MinValue;
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
        _app.ReanalyzeSelectedRequested -= OnReanalyzeSelectedRequested;
        _keyboard.Action -= OnKeyboardAction;
        _positionTimer?.Stop();
    }
}
