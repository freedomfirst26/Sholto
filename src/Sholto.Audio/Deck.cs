using Sholto.Analysis;
using Sholto.Analysis.Analyzers;
using Sholto.Analysis.Analyzers.Beats;
using Sholto.Analysis.Analyzers.Keys;
using Sholto.Analysis.Processing;
using Sholto.Analysis.Reporting;
using Sholto.Analysis.Stores;
using Sholto.Analysis.Analyzers.Vocals;
using Sholto.Analysis.Analyzers.Waveform;
using SoundFlow.Abstracts;
using SoundFlow.Enums;
using SoundFlow.Metadata.Models;
using SoundFlow.Modifiers;
using SoundFlow.Providers;
using SoundFlow.Structs;
using SfEngine = SoundFlow.Abstracts.AudioEngine;

namespace Sholto.Audio;

/// <summary>
/// One DJ deck. Holds a stable Mixer component that the AudioEngine attaches
/// to the master mixer; the SoundPlayer inside is rebuilt on each track load.
/// </summary>
public sealed class Deck : IMixSource
{
    // The six components extracted from Deck (beatgrid + loops in stage 1;
    // mixer/output + pitch/tempo in stage 2; transport in stage 3; track
    // loading + the SoundFlow graph wiring in stage 4; stems in stage 5 —
    // see the architectural review programme in ~/Projects/sholto.md). Concrete types, not the
    // interfaces, so Deck can call the internal-only members (SetDetectedBasic,
    // ResetForNewTrack, ShiftLoop, AttachModifiers, ReapplyGain, and — for
    // _loading — Player/StemProvider/ScratchProvider/SampleCount/CurrentFilePath/
    // Engine/DeckMixer) that aren't part of the public port. None holds a
    // back-reference to Deck: everything a component needs from Deck's other
    // state (the live stem provider, analysis, sample count, position, current
    // file path, scratch flag, another component's own state) is handed in as
    // narrow accessor delegates at construction, below. _loading is the one
    // exception in the other direction — see its own class doc: it OWNS the
    // fields the others only ever read (_player, the data providers, the
    // sample count/current path, Analysis), because it is the component that
    // replaces them on every load.
    private readonly DeckLooping _loops;
    private readonly DeckBeatgrid _grid;
    private readonly DeckMixer _mixer;
    private readonly DeckTempo _pitch;
    private readonly TransportControl _transport;
    private readonly TrackAnalysisRun _analysisRun;
    private readonly TrackLoading _loading;
    private readonly StemControl _stems;

    public Deck(
        IAudioFileDecoder decoder,
        IStemAnalysisStep stemAnalyzer,
        IAnalysisReporter reporter,
        IAnalysisProvider analysisProvider,
        IKeyAnalysisStore keyCache,
        IGridAdjustmentStore gridCache,
        IWaveformPeakAnalyzer peakAnalyzer,
        IKeyAnalyzer keyAnalyzer,
        IVocalRegionAnalyzer vocalRegionAnalyzer,
        IBeatgridFitter beatgridFitter,
        IReadOnlyList<Func<SfEngine, AudioFormat, SoundModifier>> effectFactories)
    {
        _loops = new DeckLooping(
            stemProvider: () => _loading.StemProvider,
            analysis: () => _loading.Analysis,
            positionFrames: () => PositionFrames,
            sampleCount: () => _loading.SampleCount);
        _loops.LoopChanged += region => LoopChanged?.Invoke(region);

        _grid = new DeckBeatgrid(
            sampleCount: () => _loading.SampleCount,
            currentFilePath: () => _loading.CurrentFilePath,
            analysis: () => _loading.Analysis,
            raiseAnalysisUpdated: () => AnalysisUpdated?.Invoke(),
            shiftLoop: _loops.ShiftLoop,
            gridCache: gridCache,
            fitter: beatgridFitter);
        _grid.GridNudgedChanged += v => GridNudgedChanged?.Invoke(v);

        // DeckTempo is a pure model — it only computes PlaybackSpeed and
        // announces changes. Arbitration (who wins the shared
        // IVarispeedProvider.SetSpeed target — tempo or a platter scratch)
        // is Deck's job: it already owns the provider and _scratching (see
        // ScratchRate/EndScratch below), so it pushes here and skips while a
        // scratch is in flight, exactly as DeckTempo itself used to do.
        _mixer = new DeckMixer(
            analysis: () => _loading.Analysis,
            playbackSpeed: () => _pitch.PlaybackSpeed);
        _mixer.CueChanged += active => CueChanged?.Invoke(active);

        _pitch = new DeckTempo();
        _pitch.PlaybackSpeedChanged += speed =>
        {
            if (!_scratching) VarispeedProvider?.SetSpeed(speed);
            _mixer.OnPlaybackSpeedChanged(speed);
        };
        // Echo tempo-tracking's other half: BPM can land seconds after the
        // echo is switched on (analysis is async), so also re-derive on every
        // AnalysisUpdated, not just on tempo-fader moves. See DeckMixer.
        AnalysisUpdated += _mixer.OnAnalysisUpdated;

        _transport = new TransportControl(
            player: () => _loading.Player,
            positionFrames: () => PositionFrames);

        _stems = new StemControl(stemProvider: () => _loading.StemProvider);

        // Analysis orchestration (BPM/key/stems), extracted out of TrackLoading —
        // see TrackAnalysisRun's class doc. Constructed before _loading: its
        // onStemsReady/setSampleCount delegates below close over the _loading
        // field itself (assigned right after), not its value, so they're safe to
        // wire before _loading exists — by the time either delegate actually runs
        // (async, well after this constructor returns) _loading is set.
        _analysisRun = new TrackAnalysisRun(
            analysisProvider: analysisProvider,
            keyCache: keyCache,
            keyAnalyzer: keyAnalyzer,
            stemAnalyzer: stemAnalyzer,
            peakAnalyzer: peakAnalyzer,
            vocalRegionAnalyzer: vocalRegionAnalyzer,
            reporter: reporter,
            decoder: decoder,
            setDetectedBasic: _grid.SetDetectedBasic,
            setSampleCount: count => _loading.SetSampleCount(count),
            onStemsReady: stems => _loading.SwitchToStemMode(stems));

        // See TrackLoading's class doc for why this component OWNS _player/
        // the data providers/Analysis/etc. rather than taking them as
        // accessors like every component above. Its write-backs: the grid's
        // and loop's internal-only reset hooks, the mixer's ReapplyGain/
        // AttachModifiers, the live tempo, and — the one write into Deck
        // itself — resetScratching, since the scratch cluster (untouched
        // this stage) still owns _scratching.
        _loading = new TrackLoading(
            decoder: decoder,
            analysisRun: _analysisRun,
            effectFactories: effectFactories,
            resetForNewTrack: _grid.ResetForNewTrack,
            exitLoop: _loops.ExitLoop,
            attachModifiers: _mixer.AttachModifiers,
            reapplyGain: _mixer.ReapplyGain,
            playbackSpeed: () => _pitch.PlaybackSpeed,
            resetScratching: () => _scratching = false);
        _loading.AnalysisUpdated += () => AnalysisUpdated?.Invoke();
    }

    /// <summary>
    /// Layered analysis cache (memory → db → compute). Constructor-injected —
    /// never null, never half-built; see <see cref="TrackLoading"/>.
    /// </summary>
    public IAnalysisProvider AnalysisProvider => _loading.AnalysisProvider;

    /// <summary>True once any nudge has been applied to the current
    /// track's grid — used by the waveform to recolour the loop band
    /// red so the user can see at a glance "I've tuned this loop, it
    /// isn't on madmom's default grid." Resets on BeginLoad /
    /// ResetControls so the next track starts clean.</summary>
    public bool IsGridNudged => _grid.IsGridNudged;
    public event Action<bool>? GridNudgedChanged;

    /// <summary>
    /// Shared reporter — receives waveform / beats / stems progress events.
    /// Constructor-injected, same reasoning as <see cref="AnalysisProvider"/>.
    /// </summary>
    public IAnalysisReporter Reporter => _loading.Reporter;

    /// <summary>Stem separation (demucs by default). Optional; playback just stays on
    /// the mixed track when the tool isn't installed. Typed as the role interface, not
    /// the concrete analyser, so a test harness can drop in a fake without a real
    /// subprocess. Constructor-injected — built once in App.axaml.cs and shared
    /// across both decks; no per-Deck default.</summary>
    public IStemAnalysisStep StemAnalyzer => _loading.StemAnalyzer;

    public TrackAnalysis Analysis => _loading.Analysis;

    /// <summary>True once this deck has auto-switched to stem-mix playback
    /// (see <c>TrackLoading.SwitchToStemMode</c>). Exposed read-only for state
    /// snapshots (e.g. Sholto.Bench) — not used by playback itself.</summary>
    internal bool StemsLoaded => _loading.StemsLoaded;

    /// <summary>The file path passed to the most recent <see cref="Load"/> or
    /// <see cref="LoadStreaming"/>, or null if nothing has been loaded yet.
    /// Exposed read-only for state snapshots (e.g. Sholto.Bench).</summary>
    internal string? CurrentFilePath => _loading.CurrentFilePath;

    /// <summary>Whichever varispeed-capable provider currently backs the
    /// SoundPlayer, or null on the streaming (ChunkedDataProvider) path where
    /// neither exists yet. Used by both the tempo-fader pipeline
    /// (<see cref="ApplyPlaybackSpeed"/>) and the platter-scratch API below —
    /// they're mutually exclusive in time (scratch overrides the fader's rate
    /// while active, see <see cref="ScratchRate"/>), so one field of the
    /// currently-active provider is enough. <c>_loading</c> owns both
    /// providers (see <see cref="TrackLoading"/>); this just reads them back.</summary>
    private IVarispeedProvider? VarispeedProvider => (IVarispeedProvider?)_loading.StemProvider ?? _loading.ScratchProvider;

    // — Pitch (tempo) — extracted to DeckTempo (see field doc above).
    // Deck keeps only the thin pass-through surface below.

    /// <summary>The ± range the tempo fader spans. See <see cref="DeckTempo"/>.</summary>
    public double TempoRange
    {
        get => _pitch.TempoRange;
        set => _pitch.TempoRange = value;
    }

    /// <summary>0..1, 0.5 = no shift. The same value the FLX-4 fader sends.</summary>
    public double TempoPosition
    {
        get => _pitch.TempoPosition;
        set => _pitch.TempoPosition = value;
    }

    /// <summary>Half / double / unity playback multiplier driven by the BPM-click
    /// override on the deck. Compounds with the live tempo fader so the user can
    /// nudge ±6 % around the corrected speed.</summary>
    public double BpmMultiplier
    {
        get => _pitch.BpmMultiplier;
        set => _pitch.BpmMultiplier = value;
    }

    /// <summary>Live playback-speed multiplier (1.0 = unity), already factoring in
    /// both the fader's ±range shift and the BPM-click override.</summary>
    public float PlaybackSpeed => _pitch.PlaybackSpeed;

    // — Mixer / output — extracted to DeckMixer (see field doc
    // above). Deck keeps only the thin pass-through surface below.

    /// <summary>Linear gain [0..1]. Applied to the SoundPlayer so the deck's output is scaled before the master mixer sums it with the other deck.</summary>
    public float Volume
    {
        get => _mixer.Volume;
        set => _mixer.Volume = value;
    }

    public bool IsLoaded => _loading.IsLoaded;
    public bool IsPlaying => _loading.IsPlaying;

    // — Platter scratch —
    //
    // While active, the deck's effective playback rate is driven directly by
    // Orchestrator (platter velocity, signed, any magnitude) instead of by
    // PlaybackSpeed. See ScratchRate / EndScratch below.
    private bool _scratching;
    // Whether the deck was actually Playing (per the user, before we may have
    // force-started it below) at the moment the platter was first grabbed.
    // EndScratch restores this rather than assuming "playing".
    private bool _wasPlayingBeforeScratch;

    /// <summary>True once the deck is on a provider that supports signed
    /// vinyl-style speed (in-memory raw buffer or stem mix) — i.e. everywhere
    /// except the very first moment of a streamed load, before <see cref="Load"/>
    /// has swapped in a real provider. Orchestrator checks this before routing
    /// a top-platter tick into <see cref="ScratchRate"/>; when false it falls
    /// back to the old silent-seek jog behaviour.</summary>
    public bool CanScratch => VarispeedProvider is not null;

    /// <summary>Drive the deck's playback rate directly from platter velocity:
    /// <paramref name="rate"/> is a signed multiple of unity (1.0 = normal
    /// forward speed, 0 = held, negative = reverse), REPLACING — not
    /// composing with — the tempo-fader-derived <see cref="PlaybackSpeed"/>
    /// while a scratch is in flight. No-op if the deck isn't on a scratchable
    /// provider (see <see cref="CanScratch"/>).
    /// <para>SoundFlow's SoundPlayer only pulls samples from the data provider
    /// while its internal state is <c>Playing</c> (a paused player renders
    /// silence without even calling ReadBytes) — so a deck that was paused
    /// when the platter was grabbed is force-started here. That matches CDJ
    /// vinyl mode: the platter drives sound even parked, and at rate 0
    /// ReadBytes just re-emits the frame under the needle (near-silent hold,
    /// not literal silence). <see cref="EndScratch"/> restores whatever
    /// play/pause state the deck was actually in.</para></summary>
    public void ScratchRate(double rate)
    {
        var vp = VarispeedProvider;
        var player = _loading.Player;
        if (vp is null || player is null) return;
        if (!_scratching)
        {
            _scratching = true;
            _wasPlayingBeforeScratch = IsPlaying;
            if (!_wasPlayingBeforeScratch) player.Play();
        }
        vp.SetSpeed((float)rate);
    }

    /// <summary>Release the platter: hand the provider's speed back to the
    /// deck's own <see cref="PlaybackSpeed"/> (tempo fader × BPM multiplier),
    /// and restore whichever play/pause state the deck was in before the
    /// scratch began. No-op if no scratch is in flight.</summary>
    public void EndScratch()
    {
        if (!_scratching) return;
        _scratching = false;
        VarispeedProvider?.SetSpeed(PlaybackSpeed);
        // Resync the SoundPlayer's INTERNAL clock to where the scratch actually
        // left the provider. The player's _rawSamplePosition only advances by
        // frames rendered in real time — a scratch moves the provider's cursor
        // far beyond/behind that, so player.Time goes stale at roughly the
        // pre-scratch position. SeekRelative/SeekToFraction compute their targets
        // from player.Time, so the FIRST post-scratch seek (side-ring nudge,
        // quantize, minimap click) would yank playback back to where the scratch
        // began. A same-place Seek here re-bases both clocks onto the provider's
        // true position (the providers' 256-frame declick fade makes it silent).
        var player = _loading.Player;
        if (player is not null)
            player.Seek(TimeSpan.FromSeconds(
                PositionFrames / (double)AudioFileDecoder.TargetSampleRate));
        if (!_wasPlayingBeforeScratch) player?.Pause();
    }

    // Read provider.Position (raw samples consumed) directly. Source rate now
    // matches the engine rate (see AudioFileDecoder.TargetSampleRate) so this is
    // equivalent to SoundPlayer.Time, but staying on Position keeps us correct
    // if those rates ever diverge again.
    public long PositionFrames =>
        _loading.Player is null ? 0 : _loading.Player.DataProvider.Position / 2;

    public double PlayPosition
    {
        get
        {
            if (_loading.SampleCount == 0) return 0.0;
            return Math.Clamp((double)PositionFrames / _loading.SampleCount, 0.0, 1.0);
        }
    }

    public SoundComponent Component =>
        _loading.DeckMixer ?? throw new InvalidOperationException("AttachEngine must be called first.");

    /// <summary>Build the deck's own mixer and the ordered post-mix effect
    /// chain on top of it. See <see cref="TrackLoading.AttachEngine"/>.</summary>
    public void AttachEngine(SfEngine engine, AudioFormat format) => _loading.AttachEngine(engine, format);

    /// <summary>Announce "a new track is about to load". See
    /// <see cref="TrackLoading.BeginLoad"/>.</summary>
    public void BeginLoad() => _loading.BeginLoad();

    /// <summary>Reset every audio-side control to its neutral state — EQ
    /// bands to unity, filter to bypass, stems to full volume, active
    /// loop exited. Called from <c>DeckViewModel.BeginLoad</c> so a fresh
    /// track always starts with a clean signal path, no carryover from
    /// whatever the user was doing on the previous track (drums muted,
    /// vocals attenuated, filter swept down, loop running, etc.).</summary>
    public void ResetControls()
    {
        SetEq(0, 0.5);
        SetEq(1, 0.5);
        SetEq(2, 0.5);
        SetFilter(0.5);
        _stems.Reset();
        _loops.ExitLoop();
        // Flush the echo's delay line too — otherwise its ~4s tail keeps
        // repeating the OUTGOING track's audio over the incoming one. Unlike
        // a plain SetEcho(false) (which deliberately lets the tail ring out
        // when the user toggles echo off mid-track), a fresh track needs a
        // clean slate.
        _mixer.ResetEcho();
    }

    /// <summary>
    /// Start playback from <paramref name="filePath"/> via SoundFlow's
    /// <c>ChunkedDataProvider</c>. See <see cref="TrackLoading.LoadStreaming"/>.
    /// </summary>
    public void LoadStreaming(string filePath) => _loading.LoadStreaming(filePath);

    /// <summary>
    /// Synchronous load (audio starts immediately). See <see cref="TrackLoading.Load"/>.
    /// </summary>
    public void Load(string filePath, float[] stereoSamples, int sampleRate) =>
        _loading.Load(filePath, stereoSamples, sampleRate);


    // — Stems — extracted to StemControl (see field doc above). Deck keeps
    // only the thin pass-through surface below so its public API is
    // unchanged.

    /// <summary>Mute/unmute one of the 3 UI groups (drums / vocals / instrumental).
    /// Independent from <see cref="SetStemGroupLevel"/>: gain = active × level.
    /// Lock-free. See <see cref="StemControl.SetStemGroup"/>.</summary>
    public void SetStemGroup(int group, bool active) => _stems.SetStemGroup(group, active);

    /// <summary>Continuous stem-group attenuator driven by the StemLevelMode
    /// modifier + EQ knobs (HI = drums, MID = vocals, LOW = inst). Knob
    /// position (0..1) maps through a Pioneer-style "isolator kill" curve so
    /// the bottom half does all the attenuation and the bottom detent is
    /// hard-zero (≤0.04 → 0, 0.04–0.5 → linear ramp, ≥0.5 → unity / no
    /// boost). Independent from <see cref="SetStemGroup"/>: turning the knob
    /// while pad-muted updates the stored level silently; mute stays in
    /// effect. Unmuting then brings the stem back at the stored level. See
    /// <see cref="StemControl.SetStemGroupLevel"/>.</summary>
    public void SetStemGroupLevel(int group, double level) => _stems.SetStemGroupLevel(group, level);

    // — Mixer / output (continued) — MasterGain / CueActive / CueChanged /
    // ToggleCue, extracted to DeckMixer (see field doc above). Deck
    // keeps only the thin pass-through surface below, and still implements
    // IMixSource directly (MasterGain, CueActive, Component) since that's the
    // per-buffer port CueOutputRouter pulls.

    /// <summary>Master-path gain (channel × crossfade), read lock-free by
    /// CueOutputRouter.</summary>
    public float MasterGain => _mixer.MasterGain;

    /// <summary>Fires when cue membership changes. The Deck is the source of truth
    /// for cue state; the view model observes this rather than mirroring it.</summary>
    public event Action<bool>? CueChanged;
    public bool CueActive
    {
        get => _mixer.CueActive;
        set => _mixer.CueActive = value;
    }
    public void ToggleCue() => _mixer.ToggleCue();

    // — Beat loops — extracted to DeckLooping (see field doc above). Deck
    // keeps only the thin pass-through surface below so its public API is
    // unchanged.

    /// <summary>The deck's current loop, or null if none. Setting is internal;
    /// the UI subscribes to <see cref="LoopChanged"/>.</summary>
    public LoopRegion? ActiveLoop => _loops.ActiveLoop;

    /// <summary>Fires on the thread that mutated the loop (UI / MIDI). Argument
    /// is the new region or null on exit.</summary>
    public event Action<LoopRegion?>? LoopChanged;

    /// <summary>Engage an N-bar auto-loop snapped to the nearest downbeat. See
    /// <see cref="DeckLooping.EnableBeatLoop"/> for the full behaviour.</summary>
    public void EnableBeatLoop(int bars) => _loops.EnableBeatLoop(bars);

    /// <summary>Halve the active loop's length. See <see cref="DeckLooping.HalveLoop"/>.</summary>
    public void HalveLoop() => _loops.HalveLoop();

    /// <summary>Double the active loop's length. See <see cref="DeckLooping.DoubleLoop"/>.</summary>
    public void DoubleLoop() => _loops.DoubleLoop();

    /// <summary>Exit the active loop. See <see cref="DeckLooping.ExitLoop"/>.</summary>
    public void ExitLoop() => _loops.ExitLoop();

    // — Beatgrid adjustment model — extracted to DeckBeatgrid (see field
    // doc above and DeckBeatgrid.cs's header for the full model). DeckBeatgrid
    // now takes its IGridAdjustmentStore directly as a constructor parameter
    // (handed through by Deck's own constructor), so there is no pass-through
    // surface left here for Deck to expose.

    /// <summary>Apply a saved/loaded adjustment. See
    /// <see cref="DeckBeatgrid.ApplyGridAdjustment"/>.</summary>
    public void ApplyGridAdjustment(double? bpmOverride, double offsetSec) =>
        _grid.ApplyGridAdjustment(bpmOverride, offsetSec);

    /// <summary>Width adjust: change the effective BPM. See
    /// <see cref="DeckBeatgrid.AdjustBpm"/>.</summary>
    public void AdjustBpm(double deltaBpm) => _grid.AdjustBpm(deltaBpm);

    /// <summary>Coarse phase shift: move the whole grid by N beats. See
    /// <see cref="DeckBeatgrid.NudgeGrid"/>.</summary>
    public void NudgeGrid(int beats) => _grid.NudgeGrid(beats);

    /// <summary>Fine phase shift: move the whole grid by seconds. See
    /// <see cref="DeckBeatgrid.NudgeGridFine"/>.</summary>
    public void NudgeGridFine(double seconds) => _grid.NudgeGridFine(seconds);

    /// <summary>Set the grid from two clicked downbeat points. See
    /// <see cref="DeckBeatgrid.SetGridFromTwoPoints"/>.</summary>
    public void SetGridFromTwoPoints(double tA, double tB) => _grid.SetGridFromTwoPoints(tA, tB);

    /// <summary>Reset the grid to madmom's detection. See
    /// <see cref="DeckBeatgrid.ResetGrid"/>.</summary>
    public void ResetGrid() => _grid.ResetGrid();

    /// <summary>Raised on the analysis thread once BasicAnalysis completes.</summary>
    public event Action? AnalysisUpdated;

    /// <summary>Eject the current track: stop playback, detach the SoundPlayer, clear analysis.
    /// See <see cref="TrackLoading.Unload"/>.</summary>
    public void Unload() => _loading.Unload();

    // — Transport — extracted to TransportControl (see field doc above).
    // Deck keeps only the thin pass-through surface below.

    /// <summary>Start (or resume) playback. See <see cref="TransportControl.Play"/>.</summary>
    public void Play() => _transport.Play();

    /// <summary>Pause playback in place. See <see cref="TransportControl.Pause"/>.</summary>
    public void Pause() => _transport.Pause();

    /// <summary>Play if paused, pause if playing. See <see cref="TransportControl.TogglePlay"/>.</summary>
    public void TogglePlay() => _transport.TogglePlay();

    /// <summary>Seek relative to current position by +/- seconds, clamped to track bounds.
    /// See <see cref="TransportControl.SeekRelative"/> for the full behaviour (why it's
    /// driven off the provider's true cursor, not SoundPlayer.Time).</summary>
    public void SeekRelative(double seconds) => _transport.SeekRelative(seconds);

    /// <summary>Seek to an absolute fraction of the track (0..1). Used by the minimap
    /// click-to-jump. See <see cref="TransportControl.SeekToFraction"/>.</summary>
    public void SeekToFraction(double fraction) => _transport.SeekToFraction(fraction);

    /// <summary>
    /// Set one of the 3 EQ bands (0=Low, 1=Mid, 2=High). <paramref name="value"/> is 0..1,
    /// 0.5 = unity. Below 0.5 cuts down to −26 dB (full kill); above 0.5 boosts up to +6 dB.
    /// Safe to call from any thread — the audio thread sees the new gain on the next buffer.
    /// See <see cref="DeckMixer.SetEq"/>.
    /// </summary>
    public void SetEq(int band, double value) => _mixer.SetEq(band, value);

    /// <summary>Set the COLOR / FILTER knob position. 0 = full LP, 0.5 =
    /// bypass, 1 = full HP. Safe to call from any thread.</summary>
    public void SetFilter(double position) => _mixer.SetFilter(position);

    /// <summary>True while the beat-synced echo is feeding new input into its
    /// delay line (see <see cref="SetEcho"/>). Note this does NOT mean the
    /// echo is silent when false — a tail can still be ringing out.</summary>
    public bool EchoActive => _mixer.EchoActive;

    /// <summary>Toggle the beat-synced echo. Turning ON recomputes the delay
    /// time from the CURRENT effective BPM (source analysis BPM × live
    /// playback speed — 128 BPM if analysis hasn't landed yet) and starts
    /// feeding input into the line. Turning OFF stops feeding NEW input but
    /// leaves the existing tail ringing (feedback keeps decaying) — the
    /// classic echo-out. Doesn't chase live tempo-fader changes mid-echo;
    /// re-toggling picks up the current tempo again (v1 — see EchoEffect).
    /// See <see cref="DeckMixer.SetEcho"/>.</summary>
    public void SetEcho(bool on) => _mixer.SetEcho(on);

    /// <summary>Set one parameter on one chain effect, addressed by string id —
    /// the generic seam alongside <see cref="SetEq"/>/<see cref="SetFilter"/>/
    /// <see cref="SetEcho"/> for effects with no named method of their own (e.g.
    /// the beat-repeat/roll). Silently a no-op for an unknown effect id or before
    /// the chain has been attached. Safe to call from any thread.
    /// See <see cref="DeckMixer.SetParam"/>.</summary>
    public void SetParam(string effectId, int paramId, double value) => _mixer.SetParam(effectId, paramId, value);
}
