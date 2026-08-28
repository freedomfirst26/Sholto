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
                if (vm.DeckFor(j.Deck).Player.ActiveLoop is not null) break;
                // Accumulate; Tick() flushes it into one Seek per frame — each Seek
                // flushes SoundFlow's buffer, so per-event seeks (~100/s) would glitch.
                double secsPerTick = j.Source == JogSource.TopPlatter ? 0.05 : 0.00125;
                if (j.Deck == 0) _pendingJog1 += j.Delta * secsPerTick;
                else             _pendingJog2 += j.Delta * secsPerTick;
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

        vm.UpdateMagnetism();

        if (vm.Deck1.Player.IsLoaded) vm.Deck1.SyncPlayPosition();
        if (vm.Deck2.Player.IsLoaded) vm.Deck2.SyncPlayPosition();
    }

    public void Dispose()
    {
        _vm.ReanalyzeSelectedRequested -= OnReanalyzeSelectedRequested;
        _positionTimer?.Stop();
        _browseHoldTimer?.Stop();
    }
}
