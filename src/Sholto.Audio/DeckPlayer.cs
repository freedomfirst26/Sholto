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
public sealed class DeckPlayer
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

    /// <summary>
    /// Shared reporter — receives waveform / beats / stems progress events. Optional.
    /// </summary>
    public AnalysisReporter? Reporter { get; set; }

    public TrackAnalysis Analysis { get; private set; } = new();

    private BiquadEq3Band? _eq;

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
    }

    /// <summary>Announce "a new track is about to load" — resets the in-memory
    /// analysis so stale waveform/BPM/key bindings clear right away, without
    /// waiting for <see cref="Load"/> (which can't run until samples are decoded).
    /// Audio for the previous track keeps playing until Load lands; this is purely
    /// a visual reset so the deck UI matches the new track immediately on click.</summary>
    public void BeginLoad()
    {
        Analysis = new TrackAnalysis();
        AnalysisUpdated?.Invoke();
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

        Console.WriteLine($"[DeckPlayer] streaming {Path.GetFileName(filePath)} @ {provider.SampleRate}Hz; engine={_format.SampleRate}Hz; length={provider.Length} samples");

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
                        "DeckPlayer.AnalysisProvider must be set before LoadStreaming — without it, " +
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

                Console.WriteLine($"[DeckPlayer] analysis from {source}: {basic.Bpm:F1} BPM, {basic.BeatTimes.Length} beats, {basic.DownbeatTimes.Length} downbeats");
                Analysis.Set(basic);
                if (key is not null)
                {
                    Console.WriteLine($"[DeckPlayer] key: {key.KeyName} ({key.Camelot})");
                    Analysis.Set(key);
                }
                AnalysisUpdated?.Invoke();
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[DeckPlayer] background analysis failed: {ex.Message}");
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
                catch (Exception ex) { Console.WriteLine($"[DeckPlayer] key cache lookup failed: {ex.Message}"); }
            }
            var key = await KeyAnalyzer.AnalyzeAsync(filePath, samples, channels: 2,
                sampleRate: sampleRate, reporter: Reporter);
            if (KeyCachePut is not null)
            {
                try { await KeyCachePut(filePath, key); }
                catch (Exception ex) { Console.WriteLine($"[DeckPlayer] key cache write failed: {ex.Message}"); }
            }
            return key;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[DeckPlayer] key analysis failed: {ex.Message}");
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

        // EQ lives on _deckMixer (post-mix) — see AttachEngine. Don't attach here.
        _deckMixer.AddComponent(_player);
        // Pre-stems: SoundFlow has no provider with built-in vinyl speed for raw
        // float[] data, so tempo is a no-op until stems land and we switch to
        // StemMixDataProvider (which carries speed directly). Don't set
        // SoundPlayer.PlaybackSpeed — that engages WSOLA and chops the UI.
        Console.WriteLine($"[DeckPlayer] loaded {stereoSamples.Length} samples @ {sampleRate}Hz; engine={_format.SampleRate}Hz {_format.Channels}ch {_format.Format}");

        // Analysis runs off-thread; deck plays immediately, beat grid appears when ready.
        _ = Task.Run(async () =>
        {
            try
            {
                if (AnalysisProvider is null)
                    throw new InvalidOperationException(
                        "DeckPlayer.AnalysisProvider must be set before Load — without it, " +
                        "analyses can't be persisted to disk.");
                var (basic, source) = await AnalysisProvider.GetAsync(filePath, stereoSamples, sampleRate);
                Console.WriteLine($"[DeckPlayer] analysis from {source}: {basic.Bpm:F1} BPM, {basic.BeatTimes.Length} beats, {basic.DownbeatTimes.Length} downbeats");
                Analysis.Set(basic);
                AnalysisUpdated?.Invoke();
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[DeckPlayer] analysis failed: {ex.Message}");
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
                    catch (Exception ex) { Console.WriteLine($"[DeckPlayer] key cache lookup failed: {ex.Message}"); }
                }
                if (key is null)
                {
                    key = await KeyAnalyzer.AnalyzeAsync(filePath, stereoSamples, channels: 2,
                        sampleRate: sampleRate, reporter: Reporter);
                    if (KeyCachePut is not null)
                    {
                        try { await KeyCachePut(filePath, key); }
                        catch (Exception ex) { Console.WriteLine($"[DeckPlayer] key cache write failed: {ex.Message}"); }
                    }
                }
                Console.WriteLine($"[DeckPlayer] key: {key.KeyName} ({key.Camelot})");
                Analysis.Set(key);
                AnalysisUpdated?.Invoke();
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[DeckPlayer] key analysis failed: {ex.Message}");
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
                Console.WriteLine($"[DeckPlayer] stems ready: {Path.GetDirectoryName(stems.Vocals)}");
                AnalysisUpdated?.Invoke();

                // Auto-switch this deck to stem-mix playback so per-stem mute is live.
                // Skip if user already moved on to a different track in the meantime.
                if (loadedPath == filePath)
                    SwitchToStemMode(stems);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[DeckPlayer] stem analysis failed: {ex.Message}");
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
        _player.Volume = _volume;
        _player.Seek(TimeSpan.FromSeconds(Math.Max(0, posSeconds)));

        if (wasPlaying) _player.Play();
        Console.WriteLine("[DeckPlayer] switched to stem-mix playback (single player)");

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
                // normalizeBands: false — independent per-stem normalisation
                // would map each stem's bands to [0,1] of its OWN max, breaking
                // cross-stem comparison at merge time (a quiet vocal stem
                // would look as energetic as a loud drum stem in the colour
                // gradient). DeckViewModel.MergeActiveStemPeaks normalises the
                // merged result instead, so the gradient reflects real
                // relative energy across whatever stems are active.
                var pd = Task.Run(() => WaveformPeaks.Compute(drumsSamples,  channels: 2, sampleRate: sr, normalizeBands: false));
                var pv = Task.Run(() => WaveformPeaks.Compute(vocalsSamples, channels: 2, sampleRate: sr, normalizeBands: false));
                var pb = Task.Run(() => WaveformPeaks.Compute(bassSamples,   channels: 2, sampleRate: sr, normalizeBands: false));
                var po = Task.Run(() => WaveformPeaks.Compute(otherSamples,  channels: 2, sampleRate: sr, normalizeBands: false));
                Task.WaitAll(pd, pv, pb, po);
                Analysis.Set(new StemPeaks(pd.Result, pv.Result, pb.Result, po.Result));
                Console.WriteLine("[DeckPlayer] per-stem peaks computed");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[DeckPlayer] per-stem peaks failed: {ex.Message}");
            }
        });
    }

    /// <summary>Mute/unmute one of the 3 UI groups (drums / vocals / instrumental).
    /// "Instrumental" maps to both Bass and Other internally. Lock-free.</summary>
    public void SetStemGroup(int group, bool active)
    {
        if (_stemProvider is null) return;
        float gain = active ? 1f : 0f;
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
        if (_player is not null) _player.Volume = _volume;
    }

    // — Beat loops —
    //
    // The data provider does the actual sample-accurate wrap; DeckPlayer just
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

    /// <summary>Engage an N-beat auto-loop snapped to the nearest beat. If a
    /// loop is already active this toggles it off (matches the FLX-4's 4 BEAT
    /// button's "press again to exit" behaviour). No-op if the beatgrid hasn't
    /// landed yet.</summary>
    public void EnableBeatLoop(int beats)
    {
        if (_activeLoop is not null) { ExitLoop(); return; }
        if (_stemProvider is null) { Console.WriteLine("[DeckPlayer] loop: stems not loaded — ignored"); return; }
        var beatTimes = Analysis.Basic?.BeatTimes;
        if (beatTimes is null || beatTimes.Length < 2)
        {
            Console.WriteLine("[DeckPlayer] loop: beatgrid not ready — ignored");
            return;
        }

        // Loop length comes from the median beat interval (robust against
        // missing/extra beats from madmom near transitions). Median over the
        // first ~16 beats is plenty.
        double secPerBeat = MedianBeatInterval(beatTimes);
        if (secPerBeat <= 0) return;

        double nowSec = PositionFrames / (double)AudioFileDecoder.TargetSampleRate;
        double inSec = NearestBeat(beatTimes, nowSec);
        double outSec = inSec + beats * secPerBeat;

        long inSample = SecondsToInterleavedSample(inSec);
        long outSample = SecondsToInterleavedSample(outSec);
        // Clamp to track end.
        long maxSample = (long)_sampleCount * 2;
        if (outSample > maxSample) outSample = maxSample;
        if (outSample - inSample < 64) // sanity floor
        {
            Console.WriteLine("[DeckPlayer] loop: computed range too small — ignored");
            return;
        }

        var region = new LoopRegion(inSample, outSample);
        _activeLoop = region;
        _stemProvider.SetLoop(region);
        Console.WriteLine($"[DeckPlayer] loop ON: {beats} beats, {inSec:F3}s → {outSec:F3}s");
        LoopChanged?.Invoke(region);
    }

    /// <summary>Halve the active loop's length (loop-in stays, loop-out moves).
    /// Floor at 64 samples. No-op if not looping.</summary>
    public void HalveLoop()
    {
        if (_activeLoop is null || _stemProvider is null) return;
        var r = _activeLoop.Value;
        long newLen = r.LengthSamples / 2;
        if (newLen < 64) { Console.WriteLine("[DeckPlayer] loop: at minimum length"); return; }
        // Keep loop-out frame-aligned (stereo, so even).
        newLen &= ~1L;
        var next = new LoopRegion(r.StartSample, r.StartSample + newLen);
        _activeLoop = next;
        _stemProvider.SetLoop(next);
        Console.WriteLine($"[DeckPlayer] loop ½×: now {next.LengthSamples} samples");
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
        _stemProvider.SetLoop(next);
        Console.WriteLine($"[DeckPlayer] loop 2×: now {next.LengthSamples} samples");
        LoopChanged?.Invoke(next);
    }

    /// <summary>Exit the active loop; playback continues forward from the
    /// current cursor (no snap-back to loop-in). No-op if not looping.</summary>
    public void ExitLoop()
    {
        if (_activeLoop is null) return;
        _activeLoop = null;
        _stemProvider?.SetLoop(null);
        Console.WriteLine("[DeckPlayer] loop OFF");
        LoopChanged?.Invoke(null);
    }

    private long SecondsToInterleavedSample(double sec)
    {
        long s = (long)Math.Round(sec * AudioFileDecoder.TargetSampleRate) * 2;
        return s & ~1L;
    }

    private static double NearestBeat(double[] beatTimes, double pos)
    {
        // Linear scan is fine — beat counts are O(few hundred). Binary search
        // would shave microseconds nobody will feel.
        double best = beatTimes[0];
        double bestDelta = Math.Abs(beatTimes[0] - pos);
        for (int i = 1; i < beatTimes.Length; i++)
        {
            double d = Math.Abs(beatTimes[i] - pos);
            if (d < bestDelta) { best = beatTimes[i]; bestDelta = d; }
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
            Console.WriteLine("[DeckPlayer] PlayTestTone: engine not attached");
            return;
        }
        var osc = new Oscillator(_engine, _format) { Frequency = 440f, Volume = 0.3f };
        _deckMixer.AddComponent(osc);
        Console.WriteLine("[DeckPlayer] PlayTestTone: oscillator added");
        await Task.Delay(2000);
        _deckMixer.RemoveComponent(osc);
        osc.Dispose();
        Console.WriteLine("[DeckPlayer] PlayTestTone: done");
    }
    public void Pause() => _player?.Pause();

    public void TogglePlay()
    {
        if (!IsLoaded) { Console.WriteLine("[DeckPlayer] TogglePlay but no track loaded"); return; }
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
}
