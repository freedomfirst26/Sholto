using Sholto.Analysis;
using SoundFlow.Abstracts;
using SoundFlow.Components;
using SoundFlow.Enums;
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

    /// <summary>Tear down whichever player(s) are currently in the deck mixer.</summary>
    private void TearDownPlayers()
    {
        if (_player is not null)
        {
            _player.Stop();
            _deckMixer?.RemoveComponent(_player);
            _player.Dispose();
            _player = null;
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

        // Decode the 4 stems on this background task (we're off the audio thread).
        var drums  = AudioFileDecoder.Decode(stems.Drums);
        var vocals = AudioFileDecoder.Decode(stems.Vocals);
        var bass   = AudioFileDecoder.Decode(stems.Bass);
        var other  = AudioFileDecoder.Decode(stems.Other);

        var posSeconds = _player?.Time ?? 0;
        var wasPlaying = IsPlaying;

        // Tear down the original single-buffer player.
        if (_player is not null)
        {
            _player.Stop();
            _deckMixer.RemoveComponent(_player);
            _player.Dispose();
            _player = null;
        }

        var provider = new StemMixDataProvider(drums, vocals, bass, other, sampleRate: AudioFileDecoder.TargetSampleRate);
        _stemProvider = provider;
        _player = new SoundPlayer(_engine, _format, provider);
        _deckMixer.AddComponent(_player);
        // Speed is owned by the provider, not the SoundPlayer (see ApplyPlaybackSpeed).
        provider.SetSpeed(PlaybackSpeed);
        _player.Volume = _volume;
        _player.Seek(TimeSpan.FromSeconds(Math.Max(0, posSeconds)));

        if (wasPlaying) _player.Play();
        Console.WriteLine("[DeckPlayer] switched to stem-mix playback (single player)");
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

    /// <summary>Raised on the analysis thread once BasicAnalysis completes.</summary>
    public event Action? AnalysisUpdated;

    /// <summary>Eject the current track: stop playback, detach the SoundPlayer, clear analysis.</summary>
    public void Unload()
    {
        TearDownPlayers();
        _sampleCount = 0;
        Analysis = new TrackAnalysis();
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
