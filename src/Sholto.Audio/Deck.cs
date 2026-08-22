using Sholto.Analysis;
using SoundFlow.Abstracts;
using SoundFlow.Components;
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
public sealed class Deck
{
    private SfEngine? _engine;
    private AudioFormat _format;
    private Mixer? _deckMixer;
    private SoundPlayer? _player;
    // Strong reference to the SoundPlayer's current data provider so we can
    // Dispose() it explicitly when ejecting a track. SoundFlow's SoundPlayer
    // does not auto-dispose its provider, so without this the previous track's
    // float[] samples (RawDataProvider) or 4×float[] stems (StemMixDataProvider)
    // would survive until the next GC sweep, potentially holding hundreds of MB.
    private SoundFlow.Interfaces.ISoundDataProvider? _currentDataProvider;
    private int _sampleRate = 48000;
    private long _sampleCount;

    /// <summary>
    /// Layered analysis cache (memory → db → compute). If unset, falls back to
    /// computing inline on every load.
    /// </summary>
    public AnalysisProvider? AnalysisProvider { get; set; }

    /// <summary>Optional key-analysis cache hook. App.axaml.cs wires this to the
    /// SQLite-backed store so loads after the first one skip the chroma compute.</summary>
    public Func<string, Task<KeyAnalysis?>>? KeyCacheGet { get; set; }
    public Func<string, KeyAnalysis, Task>? KeyCachePut { get; set; }

    // Current track's file path — captured at Load / LoadStreaming so grid
    // adjustments can be persisted per-track via GridAdjustmentPut.
    private string? _currentFilePath;

    /// <summary>True once any nudge has been applied to the current
    /// track's grid — used by the waveform to recolour the loop band
    /// red so the user can see at a glance "I've tuned this loop, it
    /// isn't on madmom's default grid." Resets on BeginLoad /
    /// ResetControls so the next track starts clean.</summary>
    public bool IsGridNudged { get; private set; }
    public event Action<bool>? GridNudgedChanged;
    private void SetGridNudged(bool v)
    {
        if (IsGridNudged == v) return;
        IsGridNudged = v;
        GridNudgedChanged?.Invoke(v);
    }

    /// <summary>
    /// Shared reporter — receives waveform / beats / stems progress events. Optional.
    /// </summary>
    public AnalysisReporter? Reporter { get; set; }

    public TrackAnalysis Analysis { get; private set; } = new();

    private BiquadEq3Band? _eq;
    private DjFilter? _filter;

    // Stem playback: when stems are available we swap the SoundPlayer's data provider
    // to a StemMixDataProvider that owns the 4 decoded stem buffers and mixes them
    // on demand. The audio path stays single-player; per-stem mute is just a
    // lock-free gain write inside that provider. No extra SoundPlayers, no extra
    // mixer summing, no extra resamplers.
    private StemMixDataProvider? _stemProvider;
    private bool InStemMode => _stemProvider is not null;

    // Pitch (tempo) state. PitchRange is the ±range the fader spans (0.06 = ±6%).
    // TempoPosition is the fader position 0..1 (0.5 = no shift). Effective playback
    // speed = 1 + (TempoPosition - 0.5) * 2 * PitchRange.
    // Pioneer convention: position 0.0 = top of fader = slower (negative shift),
    // position 1.0 = bottom = faster. We invert so that "higher position = faster"
    // matches the visual intuition of moving the fader down.
    private double _pitchRange = 0.06;          // ±6% default
    private double _tempoPosition = 0.5;        // centred = unity speed
    private double _bpmMultiplier = 1.0;        // ½ / ×2 audio multiplier from BPM click

    public double PitchRange
    {
        get => _pitchRange;
        set { _pitchRange = Math.Max(0, value); ApplyPlaybackSpeed(); }
    }

    /// <summary>0..1, 0.5 = no shift. The same value the FLX-4 fader sends.</summary>
    public double TempoPosition
    {
        get => _tempoPosition;
        set { _tempoPosition = Math.Clamp(value, 0, 1); ApplyPlaybackSpeed(); }
    }

    /// <summary>Half / double / unity playback multiplier driven by the BPM-click
    /// override on the deck. Compounds with the live tempo fader so the user can
    /// nudge ±6 % around the corrected speed.</summary>
    public double BpmMultiplier
    {
        get => _bpmMultiplier;
        set { _bpmMultiplier = value > 0 ? value : 1.0; ApplyPlaybackSpeed(); }
    }

    /// <summary>Live playback-speed multiplier (1.0 = unity), already factoring in
    /// both the fader's ±range shift and the BPM-click override.</summary>
    public float PlaybackSpeed { get; private set; } = 1.0f;

    private void ApplyPlaybackSpeed()
    {
        // Top of fader (pos=0) → slowdown, bottom (pos=1) → speedup.
        // (-1 + 2 * pos) maps 0..1 → -1..+1, then scaled by range.
        // Multiplied by BpmMultiplier so a halved track plays at half speed.
        // Jog nudge adds on top for transient pitch-bends from the outer ring.
        double fader = 1.0 + (-1.0 + 2.0 * _tempoPosition) * _pitchRange;
        PlaybackSpeed = (float)(fader * _bpmMultiplier);

        // Push the speed into our own provider — NOT into SoundFlow.SoundPlayer.PlaybackSpeed.
        // SoundFlow's PlaybackSpeed engages WSOLA time-stretching, which allocates
        // per audio block and triggers frequent GC pauses that freeze the UI.
        // StemMixDataProvider does plain linear-interp resampling instead: vinyl
        // mode, pitch shifts with speed, no allocations on the hot path.
        _stemProvider?.SetSpeed(PlaybackSpeed);
    }

    private float _volume = 1.0f;
    /// <summary>Linear gain [0..1]. Applied to the SoundPlayer so the deck's output is scaled before the master mixer sums it with the other deck.</summary>
    public float Volume
    {
        get => _volume;
        set
        {
            _volume = Math.Clamp(value, 0f, 1f);
            ApplyDeckGain();
        }
    }

    public bool IsLoaded => _player is not null;
    public bool IsPlaying => _player?.State == PlaybackState.Playing;

    // Read provider.Position (raw samples consumed) directly. Source rate now
    // matches the engine rate (see AudioFileDecoder.TargetSampleRate) so this is
    // equivalent to SoundPlayer.Time, but staying on Position keeps us correct
    // if those rates ever diverge again.
    public long PositionFrames =>
        _player is null ? 0 : _player.DataProvider.Position / 2;

    public double PlayPosition
    {
        get
        {
            if (_sampleCount == 0) return 0.0;
            return Math.Clamp((double)PositionFrames / _sampleCount, 0.0, 1.0);
        }
    }

    public SoundComponent Component =>
        _deckMixer ?? throw new InvalidOperationException("AttachEngine must be called first.");

    public void AttachEngine(SfEngine engine, AudioFormat format)
    {
        _engine = engine;
        _format = format;
        _deckMixer = new Mixer(engine, format);

        // EQ is post-mix: a single instance processes the deck's summed signal.
        // Putting one EqualizerBand-stateful filter on multiple players (one per
        // stem) lets the 4 streams trample each other's biquad state — that
        // shows up as scratchy / clipping audio.
        _eq = new BiquadEq3Band(engine, format);
        _deckMixer.AddModifier(_eq);

        // Filter sits POST-EQ — matches Pioneer mixer channel-strip order
        // (EQ → COLOR knob → fader). One-knob LP/HP sweep around a center
        // dead-zone; see DjFilter.
        _filter = new DjFilter(engine, format);
        _deckMixer.AddModifier(_filter);
    }

    /// <summary>Announce "a new track is about to load" — resets the in-memory
    /// analysis so stale waveform/BPM/key bindings clear right away, without
    /// waiting for <see cref="Load"/> (which can't run until samples are decoded).
    /// Audio for the previous track keeps playing until Load lands; this is purely
    /// a visual reset so the deck UI matches the new track immediately on click.</summary>
    public void BeginLoad()
    {
        Analysis = new TrackAnalysis();
        // Fresh track: drop the previous track's detection + adjustment so a
        // late-arriving regen can't apply the old offset to the new grid. The
        // new track's saved adjustment (if any) is re-applied by the load path
        // via ApplyGridAdjustment once its detection lands.
        _detectedBasic = null;
        _bpmOverride = null;
        _offsetSec = 0.0;
        SetGridNudged(false);
        AnalysisUpdated?.Invoke();
    }

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
        _filter?.SetPosition(0.5f);
        for (int g = 0; g < 3; g++)
        {
            _groupActive[g] = true;
            _groupLevel[g]  = 1f;
            ApplyGroupGain(g);
        }
        if (_activeLoop is not null)
        {
            _activeLoop = null;
            _stemProvider?.SetLoop(null);
            LoopChanged?.Invoke(null);
        }
    }

    /// <summary>
    /// Start playback from <paramref name="filePath"/> via SoundFlow's
    /// <see cref="ChunkedDataProvider"/>. Audio is playable within ~100 ms (file
    /// open + first chunk decode) regardless of track length. The full file is
    /// then decoded once in the background — solely for analysis (BPM, beats,
    /// key, waveform peaks). Analysis results land via the per-type
    /// <see cref="TrackAnalysis"/> events some seconds later.
    /// Memory footprint while playing: a couple of chunks worth of native
    /// PCM inside ChunkedDataProvider, no full mixed buffer.
    /// </summary>
    public void LoadStreaming(string filePath)
    {
        if (_engine is null || _deckMixer is null)
            throw new InvalidOperationException("AttachEngine must be called first.");

        Analysis = new TrackAnalysis();

        TearDownPlayers();

        // ChunkedDataProvider owns the FileStream and disposes it as part of its
        // own Dispose. We pass minimal ReadOptions — no tag/album-art parsing
        // (track metadata already lives in the library scan).
        var fileStream = File.Open(filePath, FileMode.Open, FileAccess.Read, FileShare.Read);
        var provider = new ChunkedDataProvider(_engine, fileStream,
            new ReadOptions { ReadTags = false, ReadAlbumArt = false },
            chunkSize: 32768);

        _currentDataProvider = provider;
        _sampleRate = provider.SampleRate;
        _sampleCount = provider.Length > 0 ? provider.Length / 2 : 0;

        _player = new SoundPlayer(_engine, _format, provider);
        _deckMixer.AddComponent(_player);
        // Carry the channel fader's current attenuation forward — otherwise
        // SoundPlayer defaults to 1.0 and a deck loaded with the fader down
        // bursts in at full volume until the user touches the fader.
        _player.Volume = 1.0f; // unity — master gain applied downstream by the router (pre-fader cue tap)
        Volatile.Write(ref _masterGain, _volume);
        _currentFilePath = filePath;

        Console.WriteLine($"[Deck] streaming {Path.GetFileName(filePath)} @ {provider.SampleRate}Hz; engine={_format.SampleRate}Hz; length={provider.Length} samples");

        KickOffAnalysisFor(filePath);
    }

    /// <summary>Run BPM + key analysis on this track in the background. Decodes
    /// the file once and feeds both pipelines, then drops the decoded buffer.
    /// Called by <see cref="LoadStreaming"/>; safe to call repeatedly (each
    /// invocation gets its own captured filePath).</summary>
    private void KickOffAnalysisFor(string filePath)
    {
        _ = Task.Run(async () =>
        {
            float[]? samples = null;
            try
            {
                if (AnalysisProvider is null)
                    throw new InvalidOperationException(
                        "Deck.AnalysisProvider must be set before LoadStreaming — without it, " +
                        "analyses can't be persisted to disk.");

                // Decode once for both basic and key analysis. After both finish
                // we drop the reference so the ~92 MB float[] can be GC'd.
                samples = AudioFileDecoder.Decode(filePath);
                int sampleRate = AudioFileDecoder.TargetSampleRate;

                // Update the visible sample count now that we have the exact value.
                _sampleCount = samples.Length / 2;

                var basicTask = AnalysisProvider.GetAsync(filePath, samples, sampleRate);
                var keyTask   = ComputeKeyAsync(filePath, samples, sampleRate);
                await Task.WhenAll(basicTask, keyTask);

                var (basic, source) = await basicTask;
                var key = await keyTask;

                Console.WriteLine($"[Deck] analysis from {source}: {basic.Bpm:F1} BPM, {basic.BeatTimes.Length} beats, {basic.DownbeatTimes.Length} downbeats");
                SetDetectedBasic(basic);
                if (key is not null)
                {
                    Console.WriteLine($"[Deck] key: {key.KeyName} ({key.Camelot})");
                    Analysis.Set(key);
                }
                AnalysisUpdated?.Invoke();
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[Deck] background analysis failed: {ex.Message}");
            }
            finally
            {
                // Drop our reference to the decoded buffer. The GetAsync / KeyAnalyzer
                // calls have already consumed what they need; nothing else holds it.
                samples = null;
            }
        });
    }

    private async Task<KeyAnalysis?> ComputeKeyAsync(string filePath, float[] samples, int sampleRate)
    {
        try
        {
            if (KeyCacheGet is not null)
            {
                try { var cached = await KeyCacheGet(filePath); if (cached is not null) return cached; }
                catch (Exception ex) { Console.WriteLine($"[Deck] key cache lookup failed: {ex.Message}"); }
            }
            var key = await KeyAnalyzer.AnalyzeAsync(filePath, samples, channels: 2,
                sampleRate: sampleRate, reporter: Reporter);
            if (KeyCachePut is not null)
            {
                try { await KeyCachePut(filePath, key); }
                catch (Exception ex) { Console.WriteLine($"[Deck] key cache write failed: {ex.Message}"); }
            }
            return key;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[Deck] key analysis failed: {ex.Message}");
            return null;
        }
    }

    /// <summary>
    /// Synchronous load (audio starts immediately). Beat analysis is kicked off in
    /// the background; the AnalysisUpdated callback fires once it completes so the
    /// view model can re-bake the waveform with real beats.
    /// </summary>
    public void Load(string filePath, float[] stereoSamples, int sampleRate)
    {
        if (_engine is null || _deckMixer is null)
            throw new InvalidOperationException("AttachEngine must be called first.");

        Analysis = new TrackAnalysis();
        _sampleRate = sampleRate;
        _sampleCount = stereoSamples.Length / 2;

        TearDownPlayers();

        var provider = new RawDataProvider(stereoSamples, sampleRate);
        _currentDataProvider = provider;
        _player = new SoundPlayer(_engine, _format, provider);
        // Carry the channel fader's current attenuation forward — otherwise
        // SoundPlayer defaults to 1.0 and a deck loaded with the fader down
        // bursts in at full volume until the user touches the fader.
        _player.Volume = 1.0f; // unity — master gain applied downstream by the router (pre-fader cue tap)
        Volatile.Write(ref _masterGain, _volume);
        _currentFilePath = filePath;

        // EQ lives on _deckMixer (post-mix) — see AttachEngine. Don't attach here.
        _deckMixer.AddComponent(_player);
        // Pre-stems: SoundFlow has no provider with built-in vinyl speed for raw
        // float[] data, so tempo is a no-op until stems land and we switch to
        // StemMixDataProvider (which carries speed directly). Don't set
        // SoundPlayer.PlaybackSpeed — that engages WSOLA and chops the UI.
        Console.WriteLine($"[Deck] loaded {stereoSamples.Length} samples @ {sampleRate}Hz; engine={_format.SampleRate}Hz {_format.Channels}ch {_format.Format}");

        // Analysis runs off-thread; deck plays immediately, beat grid appears when ready.
        _ = Task.Run(async () =>
        {
            try
            {
                if (AnalysisProvider is null)
                    throw new InvalidOperationException(
                        "Deck.AnalysisProvider must be set before Load — without it, " +
                        "analyses can't be persisted to disk.");
                var (basic, source) = await AnalysisProvider.GetAsync(filePath, stereoSamples, sampleRate);
                Console.WriteLine($"[Deck] analysis from {source}: {basic.Bpm:F1} BPM, {basic.BeatTimes.Length} beats, {basic.DownbeatTimes.Length} downbeats");
                SetDetectedBasic(basic);
                AnalysisUpdated?.Invoke();
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[Deck] analysis failed: {ex.Message}");
            }
        });

        // Key estimation is independent of beats and stems — reads the same decoded
        // buffer the basic analysis used. Goertzel + Krumhansl-Schmuckler in-process,
        // no subprocess. Cached to the SQLite analyses table; on cache hit we skip the
        // chroma compute and just publish.
        _ = Task.Run(async () =>
        {
            try
            {
                KeyAnalysis? key = null;
                if (KeyCacheGet is not null)
                {
                    try { key = await KeyCacheGet(filePath); }
                    catch (Exception ex) { Console.WriteLine($"[Deck] key cache lookup failed: {ex.Message}"); }
                }
                if (key is null)
                {
                    key = await KeyAnalyzer.AnalyzeAsync(filePath, stereoSamples, channels: 2,
                        sampleRate: sampleRate, reporter: Reporter);
                    if (KeyCachePut is not null)
                    {
                        try { await KeyCachePut(filePath, key); }
                        catch (Exception ex) { Console.WriteLine($"[Deck] key cache write failed: {ex.Message}"); }
                    }
                }
                Console.WriteLine($"[Deck] key: {key.KeyName} ({key.Camelot})");
                Analysis.Set(key);
                AnalysisUpdated?.Invoke();
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[Deck] key analysis failed: {ex.Message}");
            }
        });

        // Stems run independently of the BPM pipeline — slower (demucs takes 30-180s
        // on CPU for one track) and isolated from playback. Cached on disk so we only
        // pay the cost the first time a track is loaded ever.
        var loadedPath = filePath;
        _ = Task.Run(async () =>
        {
            try
            {
                var stems = await DemucsStemAnalyzer.AnalyzeAsync(filePath, Reporter);
                Analysis.Set(stems);
                Console.WriteLine($"[Deck] stems ready: {Path.GetDirectoryName(stems.Vocals)}");
                AnalysisUpdated?.Invoke();

                // Auto-switch this deck to stem-mix playback so per-stem mute is live.
                // Skip if user already moved on to a different track in the meantime.
                if (loadedPath == filePath)
                    SwitchToStemMode(stems);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[Deck] stem analysis failed: {ex.Message}");
            }
        });
    }

    /// <summary>Tear down whichever player(s) are currently in the deck mixer
    /// and aggressively release the previous track's heavy memory. For a 4-min
    /// stereo track at 48 kHz this is ~92 MB in the mixed-buffer mode, and up
    /// to ~370 MB in stem mode (4 × stems). Without explicit disposal here,
    /// the float[] backing each data provider survives until the next GC sweep,
    /// which on a long DJ set produces noticeable RAM creep across track changes.</summary>
    private void TearDownPlayers()
    {
        if (_player is not null)
        {
            _player.Stop();
            _deckMixer?.RemoveComponent(_player);
            _player.Dispose();
            _player = null;
        }
        // Dispose the data provider before nulling so the provider's own
        // cleanup (e.g. StemMixDataProvider null-outs the 4 stem buffers) runs.
        if (_currentDataProvider is not null)
        {
            try { _currentDataProvider.Dispose(); } catch { /* best-effort */ }
            _currentDataProvider = null;
        }
        _stemProvider = null;
        // Per-stem mute state lives inside StemMixDataProvider; reset by virtue
        // of dropping the reference. A fresh load builds a fresh provider with
        // all gains at 1.0.
    }

    /// <summary>Swap the SoundPlayer's data provider for a <see cref="StemMixDataProvider"/>
    /// that owns the 4 decoded stems and mixes them on demand. One player, one
    /// resampler, one position — same cost as single-track playback.</summary>
    private void SwitchToStemMode(StemPaths stems)
    {
        if (_engine is null || _deckMixer is null) return;

        // Decode the 4 stems in parallel. Single-track Decode is ~1–3 s on a
        // 4-minute MP3, so doing them serially was a ~5× multiplier on stem load.
        // Task.Run lets the thread pool fan them out across cores; WhenAll
        // joins back when the slowest finishes.
        var dT = Task.Run(() => AudioFileDecoder.Decode(stems.Drums));
        var vT = Task.Run(() => AudioFileDecoder.Decode(stems.Vocals));
        var bT = Task.Run(() => AudioFileDecoder.Decode(stems.Bass));
        var oT = Task.Run(() => AudioFileDecoder.Decode(stems.Other));
        Task.WaitAll(dT, vT, bT, oT);
        var drums  = dT.Result;
        var vocals = vT.Result;
        var bass   = bT.Result;
        var other  = oT.Result;

        var posSeconds = _player?.Time ?? 0;
        var wasPlaying = IsPlaying;

        // Tear down the original single-buffer player AND dispose its data
        // provider so the previous track's full-mix float[] (held by the
        // RawDataProvider) can be reclaimed by GC immediately.
        if (_player is not null)
        {
            _player.Stop();
            _deckMixer.RemoveComponent(_player);
            _player.Dispose();
            _player = null;
        }
        if (_currentDataProvider is not null)
        {
            try { _currentDataProvider.Dispose(); } catch { /* best-effort */ }
            _currentDataProvider = null;
        }

        var provider = new StemMixDataProvider(drums, vocals, bass, other, sampleRate: AudioFileDecoder.TargetSampleRate);
        _stemProvider = provider;
        _currentDataProvider = provider;
        _player = new SoundPlayer(_engine, _format, provider);
        _deckMixer.AddComponent(_player);
        // Speed is owned by the provider, not the SoundPlayer (see ApplyPlaybackSpeed).
        provider.SetSpeed(PlaybackSpeed);
        _player.Volume = 1.0f; // unity — master gain applied downstream by the router (pre-fader cue tap)
        Volatile.Write(ref _masterGain, _volume);
        _player.Seek(TimeSpan.FromSeconds(Math.Max(0, posSeconds)));

        if (wasPlaying) _player.Play();
        Console.WriteLine("[Deck] switched to stem-mix playback (single player)");

        // Compute per-stem waveform peaks in the background. Each
        // WaveformPeaks.Compute is ~100-200 ms for a 4-min track; fan all four
        // out across cores so the slowest dictates total time. Once landed,
        // Analysis.Set fires StemPeaksReady → deck VM re-emits Peaks → waveform
        // rebakes against the current active-stem mask.
        var drumsSamples  = drums;
        var vocalsSamples = vocals;
        var bassSamples   = bass;
        var otherSamples  = other;
        _ = Task.Run(() =>
        {
            try
            {
                int sr = AudioFileDecoder.TargetSampleRate;
                // NOTE: StemPeaks are no longer consumed by the UI — the main
                // waveform is a frequency-band view with a stable silhouette, so
                // stem toggles don't repaint it. This block is dead weight kept
                // for now; removing the per-stem peak computation is a pending
                // follow-up. normalizeBands stays false so, if it is ever revived,
                // the stems remain comparable to each other (per-stem [0,1]
                // normalisation would make a quiet vocal look as loud as a kick).
                var pd = Task.Run(() => WaveformPeaks.Compute(drumsSamples,  channels: 2, sampleRate: sr, normalizeBands: false));
                var pv = Task.Run(() => WaveformPeaks.Compute(vocalsSamples, channels: 2, sampleRate: sr, normalizeBands: false));
                var pb = Task.Run(() => WaveformPeaks.Compute(bassSamples,   channels: 2, sampleRate: sr, normalizeBands: false));
                var po = Task.Run(() => WaveformPeaks.Compute(otherSamples,  channels: 2, sampleRate: sr, normalizeBands: false));
                Task.WaitAll(pd, pv, pb, po);
                Analysis.Set(new StemPeaks(pd.Result, pv.Result, pb.Result, po.Result));
                Console.WriteLine("[Deck] per-stem peaks computed");

                // Feed the vocal stem layer through the region analyzer and publish
                // the regions to the deck view (green "vocals present" rectangles).
                var regions = VocalRegionAnalyzer.Analyze(pv.Result, sr);
                Analysis.Set(regions);
                Console.WriteLine($"[Deck] vocal regions: {regions.Regions.Count}");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[Deck] per-stem peaks failed: {ex.Message}");
            }
        });
    }

    // Per-group state for the multiplicative model. Audio gain is always
    // `active ? curvedLevel : 0` so the pad-mute and the level knob act as
    // independent attenuators — same as a Pioneer mixer's channel-mute +
    // channel-fader. Pad-unmute restores whatever level was last set on the
    // knob (no auto-restore-to-unity); if both are zero, audio stays silent
    // and the user has to turn the knob to hear anything (the knob is the
    // source of truth for "where this stem should live").
    private readonly bool[]  _groupActive = { true, true, true };
    private readonly float[] _groupLevel  = { 1f, 1f, 1f };

    /// <summary>Mute/unmute one of the 3 UI groups (drums / vocals / instrumental).
    /// Independent from <see cref="SetStemGroupLevel"/>: gain = active × level.
    /// Lock-free.</summary>
    public void SetStemGroup(int group, bool active)
    {
        if (_stemProvider is null || (uint)group >= 3) return;
        _groupActive[group] = active;
        ApplyGroupGain(group);
    }

    /// <summary>Continuous stem-group attenuator driven by the StemLevelMode
    /// modifier + EQ knobs (HI = drums, MID = vocals, LOW = inst). Knob
    /// position (0..1) maps through a Pioneer-style "isolator kill" curve so
    /// the bottom half does all the attenuation and the bottom detent is
    /// hard-zero (≤0.04 → 0, 0.04–0.5 → linear ramp, ≥0.5 → unity / no
    /// boost). Independent from <see cref="SetStemGroup"/>: turning the knob
    /// while pad-muted updates the stored level silently; mute stays in
    /// effect. Unmuting then brings the stem back at the stored level.</summary>
    public void SetStemGroupLevel(int group, double level)
    {
        if (_stemProvider is null || (uint)group >= 3) return;
        float curved;
        if (level <= 0.04)      curved = 0f;
        else if (level >= 0.5)  curved = 1f;
        else                    curved = (float)((level - 0.04) / (0.5 - 0.04));
        _groupLevel[group] = curved;
        ApplyGroupGain(group);
    }

    private void ApplyGroupGain(int group)
    {
        if (_stemProvider is null) return;
        float gain = _groupActive[group] ? _groupLevel[group] : 0f;
        switch (group)
        {
            case 0: _stemProvider.SetGain(StemMixDataProvider.Drums,  gain); break;
            case 1: _stemProvider.SetGain(StemMixDataProvider.Vocals, gain); break;
            default:
                _stemProvider.SetGain(StemMixDataProvider.Bass,  gain);
                _stemProvider.SetGain(StemMixDataProvider.Other, gain);
                break;
        }
    }

    /// <summary>Apply Volume to the player. (Stem-level mute is handled inside the
    /// data provider.)</summary>
    private void ApplyDeckGain()
    {
        // The channel × crossfade gain no longer scales the SoundPlayer — that
        // would apply it BEFORE the cue tap. It becomes the master-path gain,
        // read lock-free per buffer by CueOutputRouter. The SoundPlayer stays at
        // unity so the headphone cue mix is genuinely pre-fader (PFL).
        Volatile.Write(ref _masterGain, _volume);
    }

    // Master-path gain (channel × crossfade), read lock-free by CueOutputRouter.
    private float _masterGain = 1.0f;
    public float MasterGain => Volatile.Read(ref _masterGain);

    // PFL cue selection — toggled by the deck CUE button. When true this deck is
    // summed into the headphone (ch3-4) mix at full level, pre-fader.
    private volatile bool _cueActive;
    /// <summary>Fires when cue membership changes. The Deck is the source of truth
    /// for cue state; the view model observes this rather than mirroring it.</summary>
    public event Action<bool>? CueChanged;
    public bool CueActive
    {
        get => _cueActive;
        set { if (_cueActive == value) return; _cueActive = value; CueChanged?.Invoke(value); }
    }
    public void ToggleCue() => CueActive = !CueActive;

    // — Beat loops —
    //
    // The data provider does the actual sample-accurate wrap; Deck just
    // computes where the loop in/out points sit (snapped to the beatgrid) and
    // pushes the LoopRegion in. v1 only supports the stem path — the chunked
    // / raw provider paths log and no-op until we wire wrap support there too.

    private LoopRegion? _activeLoop;

    /// <summary>The deck's current loop, or null if none. Setting is internal;
    /// the UI subscribes to <see cref="LoopChanged"/>.</summary>
    public LoopRegion? ActiveLoop => _activeLoop;

    /// <summary>Fires on the thread that mutated the loop (UI / MIDI). Argument
    /// is the new region or null on exit.</summary>
    public event Action<LoopRegion?>? LoopChanged;

    /// <summary>Engage an N-bar auto-loop snapped to the nearest downbeat. If
    /// a loop is already active this toggles it off (matches the FLX-4's 4
    /// BEAT button's "press again to exit" behaviour). No-op if the beatgrid
    /// hasn't landed yet.
    /// <para>The button is labelled "4 BEAT" on the controller but in practice
    /// — confirmed with the user's workflow — we treat the parameter as BARS,
    /// not beats. Default 4 = a 4-bar loop, which is the musically useful
    /// scale for DJing (it's a phrase, not just a beat).</para></summary>
    public void EnableBeatLoop(int bars)
    {
        if (_activeLoop is not null) { ExitLoop(); return; }
        if (_stemProvider is null) { Console.WriteLine("[Deck] loop: stems not loaded — ignored"); return; }
        var downbeats = Analysis.Basic?.DownbeatTimes;
        if (downbeats is null || downbeats.Length < 2)
        {
            Console.WriteLine("[Deck] loop: downbeat grid not ready — ignored");
            return;
        }

        double bpm = Analysis.Basic?.Bpm ?? 0;
        if (bpm <= 0)
        {
            Console.WriteLine("[Deck] loop: BPM unknown — ignored");
            return;
        }

        double nowSec = PositionFrames / (double)AudioFileDecoder.TargetSampleRate;
        double secondsPerBar = 60.0 / bpm * 4.0;
        var beats = Analysis.Basic?.BeatTimes;

        // Loop-in / period strategy. Default is "beat-snap" — A/B testing
        // showed madmom's individual beat positions are reliable but its
        // downbeat (1-of-4) phase labeling drifts on some tracks (loop
        // landed on beat 2 or 4 instead of beat 1, audibly wrong rhythm).
        // Beat-snap sidesteps this by treating any beat as a valid loop-in
        // and deriving the period from N×4 consecutive beats — sample-exact
        // to madmom's beat grid, with no dependence on downbeat phase.
        //
        // SHOLTO_LOOP_MODE values:
        //   beat-snap  (DEFAULT) loop-in = nearest beat; period = beatTimes[bidx+bars*4] − beatTimes[bidx]
        //                        Falls back to downbeat-grid if BeatTimes is empty.
        //   downbeat             loop-in = nearest downbeat; period = downbeats[idx+bars]
        //                        The old default — kept for A/B testing.
        //   bpm                  loop-in = nearest downbeat; period = bars × 60/bpm × 4
        //   no-snap / tap        loop-in = current play position; period = bars × 60/bpm × 4
        var mode = Environment.GetEnvironmentVariable("SHOLTO_LOOP_MODE") ?? "beat-snap";
        double inSec, outSec;
        string source;
        switch (mode)
        {
            case "downbeat":
            {
                int inIdx = NearestBeatIndex(downbeats, nowSec);
                inSec = downbeats[inIdx];
                int outIdx = inIdx + bars;
                outSec = outIdx < downbeats.Length
                    ? downbeats[outIdx]
                    : inSec + bars * secondsPerBar;
                source = outIdx < downbeats.Length ? "downbeat-grid" : "downbeat-in/BPM-fallback";
                break;
            }
            case "bpm":
            {
                int inIdx = NearestBeatIndex(downbeats, nowSec);
                inSec = downbeats[inIdx];
                outSec = inSec + bars * secondsPerBar;
                source = "downbeat-in/BPM-period";
                break;
            }
            case "no-snap":
            case "tap":
            {
                inSec = nowSec;
                outSec = inSec + bars * secondsPerBar;
                source = "no-snap/BPM-period";
                break;
            }
            case "beat-snap":
            default:
            {
                if (beats is not null && beats.Length >= bars * 4 + 1)
                {
                    int bIdx = NearestBeatIndex(beats, nowSec);
                    inSec = beats[bIdx];
                    int outBIdx = bIdx + bars * 4;
                    outSec = outBIdx < beats.Length
                        ? beats[outBIdx]
                        : inSec + bars * secondsPerBar;
                    source = outBIdx < beats.Length ? "beat-in/beatgrid" : "beat-in/BPM-fallback";
                }
                else
                {
                    // BeatTimes empty / short — usually means an old analysis
                    // record from before beat-level detection was wired in.
                    // Fall back to the downbeat grid so the loop still works.
                    int inIdx = NearestBeatIndex(downbeats, nowSec);
                    inSec = downbeats[inIdx];
                    int outIdx = inIdx + bars;
                    outSec = outIdx < downbeats.Length
                        ? downbeats[outIdx]
                        : inSec + bars * secondsPerBar;
                    source = outIdx < downbeats.Length ? "downbeat-grid-fallback" : "downbeat-in/BPM-fallback";
                }
                break;
            }
        }

        long inSample  = SecondsToInterleavedSample(inSec);
        long outSample = SecondsToInterleavedSample(outSec);

        long maxSample = (long)_sampleCount * 2;
        if (outSample > maxSample) outSample = maxSample;
        if (outSample - inSample < 64) // sanity floor
        {
            Console.WriteLine("[Deck] loop: computed range too small — ignored");
            return;
        }

        // Build the wrap-crossfade tail BEFORE publishing the loop region, so
        // the audio thread can't see a loop active without a tail to fade
        // with. (SetLoop's Volatile ordering writes loopEnd last; this
        // BuildLoopTail call's Volatile.Write completes happens-before.)
        _stemProvider.BuildLoopTail(outSample);

        var region = new LoopRegion(inSample, outSample);
        _activeLoop = region;
        _stemProvider.SetLoop(region);
        double actualLen = (outSample - inSample) / 2.0 / AudioFileDecoder.TargetSampleRate;
        double expectedLen = bars * secondsPerBar;
        Console.WriteLine($"[Deck] loop ON [{mode}]: {bars} bars @ {bpm:F1} BPM, {inSec:F3}s → {outSec:F3}s, length {actualLen:F3}s (expected {expectedLen:F3}s, source: {source})");
        LoopChanged?.Invoke(region);
    }

    /// <summary>Halve the active loop's length (loop-in stays, loop-out moves).
    /// Floor at 64 samples. No-op if not looping.</summary>
    public void HalveLoop()
    {
        if (_activeLoop is null || _stemProvider is null) return;
        var r = _activeLoop.Value;
        long newLen = r.LengthSamples / 2;
        if (newLen < 64) { Console.WriteLine("[Deck] loop: at minimum length"); return; }
        // Keep loop-out frame-aligned (stereo, so even).
        newLen &= ~1L;
        var next = new LoopRegion(r.StartSample, r.StartSample + newLen);
        _activeLoop = next;
        _stemProvider.BuildLoopTail(next.EndSample);
        _stemProvider.SetLoop(next);
        Console.WriteLine($"[Deck] loop ½×: now {next.LengthSamples} samples");
        LoopChanged?.Invoke(next);
    }

    /// <summary>Double the active loop's length, clamped to track end. No-op if
    /// not looping.</summary>
    public void DoubleLoop()
    {
        if (_activeLoop is null || _stemProvider is null) return;
        var r = _activeLoop.Value;
        long newLen = r.LengthSamples * 2;
        long maxSample = (long)_sampleCount * 2;
        long newOut = r.StartSample + newLen;
        if (newOut > maxSample) newOut = maxSample;
        if (newOut <= r.StartSample) return;
        var next = new LoopRegion(r.StartSample, newOut & ~1L);
        _activeLoop = next;
        _stemProvider.BuildLoopTail(next.EndSample);
        _stemProvider.SetLoop(next);
        Console.WriteLine($"[Deck] loop 2×: now {next.LengthSamples} samples");
        LoopChanged?.Invoke(next);
    }

    // ── Beatgrid adjustment model ──────────────────────────────────────
    //
    // madmom's detection (the BasicAnalysis returned by the analysis
    // provider) is treated as IMMUTABLE — we keep it in _detectedBasic and
    // never overwrite it. The grid the user actually sees is REGENERATED
    // from (effective BPM, effective anchor) where:
    //     effective BPM    = _bpmOverride ?? detected BPM
    //     effective anchor = detected first downbeat + _offsetSec
    // The pair (_bpmOverride, _offsetSec) is the entire manual correction —
    // tiny, nullable, persisted per-track via GridAdjustmentPut, and
    // reloaded on the next load. Null override + zero offset = pristine
    // detection. This is the Rekordbox/Serato model (store an anchor + BPM,
    // not a mutated array) and guarantees beats and downbeats can never
    // drift out of sync the way piecemeal array edits could.

    private BasicAnalysis? _detectedBasic;  // immutable madmom detection
    private double? _bpmOverride;            // null = use detected BPM
    private double _offsetSec;               // phase shift applied to the whole grid
    private int _detectedBeatsPerBar = 4;
    private double _detectedAnchorSec;       // detected first-downbeat phase reference

    /// <summary>Persist the manual grid correction (bpmOverride, offsetSec)
    /// for the current track. Wired in App.axaml.cs to
    /// GridAdjustmentCache.PutAsync.</summary>
    public Func<string, double?, double, Task>? GridAdjustmentPut { get; set; }

    /// <summary>Read a saved grid correction for a track (null = none).
    /// Wired to GridAdjustmentCache.TryGetAsync.</summary>
    public Func<string, Task<(double? BpmOverride, double OffsetSec)?>>? GridAdjustmentGet { get; set; }

    /// <summary>Store madmom's freshly-detected analysis as the immutable
    /// baseline, fetch any saved per-track correction, and publish the grid
    /// with that correction applied. Called from the analysis-landed paths
    /// instead of Analysis.Set(basic) directly, so a track that was hand-tuned
    /// in a previous session shows the tuned grid the moment detection lands.
    /// async void: invoked fire-and-forget from the background analysis task,
    /// same threading contract as the rest of the analysis pipeline.</summary>
    private async void SetDetectedBasic(BasicAnalysis basic)
    {
        _detectedBasic = basic;
        _detectedBeatsPerBar = InferBeatsPerBar(basic);
        _detectedAnchorSec = basic.DownbeatTimes.Length > 0 ? basic.DownbeatTimes[0] : 0.0;

        // Publish the pristine detection immediately so the grid appears with
        // no perceptible delay, then apply any saved correction once the
        // (fast, local) DB read returns. Capture the path so a track swap
        // mid-fetch can't apply the wrong track's adjustment.
        string? path = _currentFilePath;
        RegenerateGrid();

        if (GridAdjustmentGet is not null && path is not null)
        {
            try
            {
                var adj = await GridAdjustmentGet(path);
                // Bail if the user swapped tracks while we were reading.
                if (adj is not null && path == _currentFilePath)
                    ApplyGridAdjustment(adj.Value.BpmOverride, adj.Value.OffsetSec);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[Deck] grid adjustment load failed: {ex.Message}");
            }
        }
    }

    /// <summary>Apply a saved/loaded adjustment. Called by the load path once
    /// the GridAdjustment row has been read from the DB. Safe to call before
    /// OR after detection lands — RegenerateGrid no-ops until both are
    /// present, and is re-run by whichever arrives second.</summary>
    public void ApplyGridAdjustment(double? bpmOverride, double offsetSec)
    {
        _bpmOverride = bpmOverride;
        _offsetSec = offsetSec;
        bool adjusted = bpmOverride is not null || Math.Abs(offsetSec) > 1e-9;
        SetGridNudged(adjusted);
        RegenerateGrid();
    }

    private static int InferBeatsPerBar(BasicAnalysis b)
    {
        if (b.DownbeatTimes.Length >= 2 && b.BeatTimes.Length >= 2)
        {
            double beatPeriod = b.BeatTimes[1] - b.BeatTimes[0];
            double barPeriod  = b.DownbeatTimes[1] - b.DownbeatTimes[0];
            if (beatPeriod > 1e-6) return Math.Max(1, (int)Math.Round(barPeriod / beatPeriod));
        }
        return 4;
    }

    /// <summary>Rebuild beats + downbeats from the detected baseline plus the
    /// current (override, offset), publish the derived BasicAnalysis, and move
    /// any active loop to track the offset delta. No-op until detection lands.</summary>
    private void RegenerateGrid()
    {
        var detected = _detectedBasic;
        if (detected is null || detected.Bpm <= 0) return;

        double bpm = _bpmOverride ?? detected.Bpm;
        double anchor = _detectedAnchorSec + _offsetSec;
        double durationSec = _sampleCount / (double)AudioFileDecoder.TargetSampleRate;
        if (durationSec <= 0) durationSec = detected.BeatTimes.Length > 0 ? detected.BeatTimes[^1] + 1 : 1;

        var (beats, downs) = Beatgrid.SynthesizeAnchored(bpm, anchor, _detectedBeatsPerBar, durationSec);
        if (downs.Length == 0) return;

        // Derived analysis keeps the detected waveform peaks; only BPM + grid
        // arrays change. Bpm reflects the effective (possibly overridden) value
        // so loop-length math and the BPM display follow the correction.
        Analysis.Set(detected with { Bpm = bpm, BeatTimes = beats, DownbeatTimes = downs });
        AnalysisUpdated?.Invoke();
    }

    /// <summary>Width adjust: change the effective BPM by
    /// <paramref name="deltaBpm"/> and regenerate the grid spacing. Fixes the
    /// "lines up at the start, drifts off the kicks later" symptom — that's a
    /// spacing (BPM) error, not a phase one. Anchored on the detected first
    /// downbeat, so after a width change you re-align with the left/right
    /// offset nudge (matches the "width first, then alignment" workflow).</summary>
    public void AdjustBpm(double deltaBpm)
    {
        var detected = _detectedBasic;
        if (detected is null || detected.Bpm <= 0) return;
        double current = _bpmOverride ?? detected.Bpm;
        double next = Math.Clamp(current + deltaBpm, 20.0, 400.0);
        // Snapping back to exactly the detected BPM clears the override (null).
        _bpmOverride = Math.Abs(next - detected.Bpm) < 1e-6 ? null : next;
        RegenerateGrid();
        AfterAdjustment();
        Console.WriteLine($"[Deck] BPM {(_bpmOverride is null ? "→ detected" : $"override → {_bpmOverride:F2}")} (Δ{deltaBpm:+0.00;-0.00})");
    }

    /// <summary>Coarse phase shift: move the whole grid by N beats. With the
    /// regenerate model this shifts beats AND downbeats together (they can't
    /// desync), which for phase purposes is identical to "relabel the 1".</summary>
    public void NudgeGrid(int beats)
    {
        var detected = _detectedBasic;
        if (detected is null) return;
        double bpm = _bpmOverride ?? detected.Bpm;
        if (bpm <= 0) return;
        double delta = beats * 60.0 / bpm;
        ShiftLoop(delta);
        _offsetSec += delta;
        RegenerateGrid();
        AfterAdjustment();
        Console.WriteLine($"[Deck] grid offset {beats:+#;-#;0} beat ({_offsetSec*1000:+0.0;-0.0} ms total)");
    }

    /// <summary>Fine phase shift: move the whole grid by <paramref name="seconds"/>
    /// (typically ±10 ms). For sub-beat alignment.</summary>
    public void NudgeGridFine(double seconds)
    {
        if (_detectedBasic is null || seconds == 0.0) return;
        ShiftLoop(seconds);
        _offsetSec += seconds;
        RegenerateGrid();
        AfterAdjustment();
        Console.WriteLine($"[Deck] grid offset fine ({_offsetSec*1000:+0.0;-0.0} ms total)");
    }

    /// <summary>Place a downbeat (bar 1) exactly at the current playhead,
    /// keeping the current spacing. The grid is periodic, so this just sets
    /// the phase — every bar shifts so one downbeat lands on the playhead.
    /// Use it cued/paused on a kick for precise one-button alignment.</summary>
    public void SetDownbeatAtPlayhead()
    {
        if (_detectedBasic is null) return;
        double nowSec = PositionFrames / (double)AudioFileDecoder.TargetSampleRate;
        _offsetSec = nowSec - _detectedAnchorSec;
        RegenerateGrid();
        AfterAdjustment();
        Console.WriteLine($"[Deck] downbeat set at playhead {nowSec:F3}s");
    }

    /// <summary>Set the grid from two clicked downbeat points. Computes the
    /// EXACT BPM from (whole bars between the points) ÷ (time between them),
    /// then anchors the grid so the earlier point lands on a downbeat. This
    /// is the drift-free way to grid a track — pinning both ends to real kicks
    /// means a single constant BPM fits perfectly, no iterating. The bar count
    /// is rounded from the current grid, so the two points only need the
    /// existing BPM to be within ~half a bar over the span (true for any
    /// madmom detection over a multi-second gap).</summary>
    public void SetGridFromTwoPoints(double tA, double tB)
    {
        var detected = _detectedBasic;
        if (detected is null) return;
        if (tB < tA) (tA, tB) = (tB, tA);
        double span = tB - tA;
        if (span < 0.5) { Console.WriteLine("[Deck] two-point: points too close — ignored"); return; }

        double curBpm = _bpmOverride ?? detected.Bpm;
        if (curBpm <= 0) return;
        double barPeriodCur = (60.0 / curBpm) * _detectedBeatsPerBar;
        int bars = Math.Max(1, (int)Math.Round(span / barPeriodCur));

        double newBeatPeriod = (span / bars) / _detectedBeatsPerBar;
        double newBpm = Math.Clamp(60.0 / newBeatPeriod, 20.0, 400.0);

        _bpmOverride = Math.Abs(newBpm - detected.Bpm) < 1e-6 ? null : newBpm;
        _offsetSec = tA - _detectedAnchorSec;   // make tA a downbeat
        RegenerateGrid();
        AfterAdjustment();
        Console.WriteLine($"[Deck] two-point grid: {bars} bar(s) in {span:F3}s → {newBpm:F3} BPM, anchor {tA:F3}s");
    }

    /// <summary>Reset the grid to madmom's detection — clears the override and
    /// offset, deletes the persisted row via GridAdjustmentPut(null, 0).</summary>
    public void ResetGrid()
    {
        _bpmOverride = null;
        _offsetSec = 0.0;
        RegenerateGrid();
        SetGridNudged(false);
        if (GridAdjustmentPut is not null && _currentFilePath is not null)
            _ = GridAdjustmentPut(_currentFilePath, null, 0.0);
        Console.WriteLine("[Deck] grid reset to detected");
    }

    /// <summary>Shift an active loop by <paramref name="seconds"/> so it tracks
    /// a phase nudge. Out-of-bounds shifts are dropped (loop stays put).</summary>
    private void ShiftLoop(double seconds)
    {
        if (_activeLoop is null || _stemProvider is null) return;
        var current = _activeLoop.Value;
        long shiftSamples = ((long)Math.Round(seconds * AudioFileDecoder.TargetSampleRate) * 2) & ~1L;
        long newIn  = current.StartSample + shiftSamples;
        long newOut = current.EndSample   + shiftSamples;
        long maxSample = (long)_sampleCount * 2;
        if (newIn < 0 || newOut > maxSample) return;
        _stemProvider.BuildLoopTail(newOut);
        var nextRegion = new LoopRegion(newIn, newOut);
        _activeLoop = nextRegion;
        _stemProvider.SetLoop(nextRegion);
        LoopChanged?.Invoke(nextRegion);
    }

    /// <summary>Mark nudged + persist the (override, offset) pair. Clears the
    /// nudged flag (and would let the loop band go back to theme colour) when
    /// both are back to neutral.</summary>
    private void AfterAdjustment()
    {
        bool adjusted = _bpmOverride is not null || Math.Abs(_offsetSec) > 1e-9;
        SetGridNudged(adjusted);
        if (GridAdjustmentPut is not null && _currentFilePath is not null)
            _ = GridAdjustmentPut(_currentFilePath, _bpmOverride, _offsetSec);
    }

    /// <summary>Exit the active loop; playback continues forward from the
    /// current cursor (no snap-back to loop-in). No-op if not looping.</summary>
    public void ExitLoop()
    {
        if (_activeLoop is null) return;
        _activeLoop = null;
        _stemProvider?.SetLoop(null);
        Console.WriteLine("[Deck] loop OFF");
        LoopChanged?.Invoke(null);
    }

    private long SecondsToInterleavedSample(double sec)
    {
        long s = (long)Math.Round(sec * AudioFileDecoder.TargetSampleRate) * 2;
        return s & ~1L;
    }

    private static int NearestBeatIndex(double[] beatTimes, double pos)
    {
        // Linear scan is fine — beat counts are O(few hundred). Binary search
        // would shave microseconds nobody will feel.
        int best = 0;
        double bestDelta = Math.Abs(beatTimes[0] - pos);
        for (int i = 1; i < beatTimes.Length; i++)
        {
            double d = Math.Abs(beatTimes[i] - pos);
            if (d < bestDelta) { best = i; bestDelta = d; }
        }
        return best;
    }

    private static double MedianBeatInterval(double[] beatTimes)
    {
        int n = Math.Min(beatTimes.Length - 1, 16);
        if (n <= 0) return 0;
        var intervals = new double[n];
        for (int i = 0; i < n; i++) intervals[i] = beatTimes[i + 1] - beatTimes[i];
        Array.Sort(intervals);
        return intervals[n / 2];
    }

    /// <summary>Raised on the analysis thread once BasicAnalysis completes.</summary>
    public event Action? AnalysisUpdated;

    /// <summary>Eject the current track: stop playback, detach the SoundPlayer, clear analysis.</summary>
    public void Unload()
    {
        TearDownPlayers();
        _sampleCount = 0;
        Analysis = new TrackAnalysis();
        if (_activeLoop is not null) { _activeLoop = null; LoopChanged?.Invoke(null); }
        // Stem state lives inside StemMixDataProvider; TearDownPlayers drops the
        // reference, so the next track loads with all stems audible by default.
        AnalysisUpdated?.Invoke();
    }

    public void Play() => _player?.Play();

    /// <summary>DIAGNOSTIC: play a 440Hz tone via SoundFlow Oscillator for 2 seconds.</summary>
    public async Task PlayTestTone()
    {
        if (_engine is null || _deckMixer is null)
        {
            Console.WriteLine("[Deck] PlayTestTone: engine not attached");
            return;
        }
        var osc = new Oscillator(_engine, _format) { Frequency = 440f, Volume = 0.3f };
        _deckMixer.AddComponent(osc);
        Console.WriteLine("[Deck] PlayTestTone: oscillator added");
        await Task.Delay(2000);
        _deckMixer.RemoveComponent(osc);
        osc.Dispose();
        Console.WriteLine("[Deck] PlayTestTone: done");
    }
    public void Pause() => _player?.Pause();

    public void TogglePlay()
    {
        if (!IsLoaded) { Console.WriteLine("[Deck] TogglePlay but no track loaded"); return; }
        if (IsPlaying) Pause(); else Play();
    }

    /// <summary>Seek relative to current position by +/- seconds, clamped to track bounds.
    /// Works whether the deck is playing, paused, or finished.
    /// Uses SoundPlayer.Time / Duration for the math because SoundPlayer.Seek interprets
    /// its TimeSpan in the same internal time domain — mixing in our drift-free
    /// PositionFrames here would over-seek by the engine/source sample-rate ratio.</summary>
    public void SeekRelative(double seconds)
    {
        if (_player is null) return;
        double target = Math.Clamp(_player.Time + seconds, 0.0, _player.Duration);
        _player.Seek(TimeSpan.FromSeconds(target));
    }

    /// <summary>
    /// Set one of the 3 EQ bands (0=Low, 1=Mid, 2=High). <paramref name="value"/> is 0..1,
    /// 0.5 = unity. Below 0.5 cuts down to −26 dB (full kill); above 0.5 boosts up to +6 dB.
    /// Safe to call from any thread — the audio thread sees the new gain on the next buffer.
    /// </summary>
    public void SetEq(int band, double value)
    {
        // _eq is created in AttachEngine and lives on _deckMixer for the deck's lifetime.
        // If MIDI arrives before AttachEngine (shouldn't, but) just bail silently.
        if (_eq is null) return;

        // Isolator gain: 0 → mute, 0.5 → unity (1.0), 1 → +6 dB (2.0).
        // Curve is intentionally linear so the centre detent at v=0.5 reads as flat.
        double v = Math.Clamp(value, 0.0, 1.0);
        float gain = v < 0.5
            ? (float)(v * 2.0)
            : (float)(1.0 + (v - 0.5) * 2.0);
        _eq?.SetBandGain(band, gain);
    }

    /// <summary>Set the COLOR / FILTER knob position. 0 = full LP, 0.5 =
    /// bypass, 1 = full HP. Safe to call from any thread.</summary>
    public void SetFilter(double position)
        => _filter?.SetPosition((float)Math.Clamp(position, 0.0, 1.0));
}
