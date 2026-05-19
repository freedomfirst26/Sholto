using CommunityDj.Analysis;
using SoundFlow.Abstracts;
using SoundFlow.Components;
using SoundFlow.Enums;
using SoundFlow.Modifiers;
using SoundFlow.Providers;
using SoundFlow.Structs;
using SfEngine = SoundFlow.Abstracts.AudioEngine;

namespace CommunityDj.Audio;

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

    /// <summary>
    /// Shared reporter — receives waveform / beats / stems progress events. Optional.
    /// </summary>
    public AnalysisReporter? Reporter { get; set; }

    public TrackAnalysis Analysis { get; private set; } = new();

    private BiquadEq3Band? _eq;

    // Stem playback: when stems are available we tear down the single mp3 player and
    // run four SoundPlayers (drums/vocals/bass/other) in the deck mixer instead. They
    // share the engine clock so they stay sample-locked.
    private SoundPlayer[]? _stemPlayers;
    private readonly bool[] _stemActive = new bool[] { true, true, true, true };
    private const int STEM_DRUMS = 0;
    private const int STEM_VOCALS = 1;
    private const int STEM_BASS = 2;
    private const int STEM_OTHER = 3;

    private bool InStemMode => _stemPlayers is not null;
    private IEnumerable<SoundPlayer> ActivePlayers =>
        _stemPlayers is not null ? _stemPlayers : (_player is not null ? new[] { _player } : Array.Empty<SoundPlayer>());

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

    public bool IsLoaded => _player is not null || InStemMode;
    public bool IsPlaying =>
        InStemMode ? _stemPlayers![0].State == PlaybackState.Playing
                   : _player?.State == PlaybackState.Playing;

    // Read provider.Position (raw samples consumed) directly — SoundPlayer.Time
    // converts using the engine sample rate, which drifts when the source rate
    // (44.1 kHz) differs from the engine rate (48 kHz).
    public long PositionFrames =>
        InStemMode ? _stemPlayers![0].DataProvider.Position / 2 :
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

        // 3-band EQ in the deck's audio path. The EQ instance is reused across loads
        // so current pot positions carry over to the next track.
        _eq ??= new BiquadEq3Band(_engine, _format);
        _player.AddModifier(_eq);
        _deckMixer.AddComponent(_player);
        Console.WriteLine($"[DeckPlayer] loaded {stereoSamples.Length} samples @ {sampleRate}Hz; engine={_format.SampleRate}Hz {_format.Channels}ch {_format.Format}");

        // Analysis runs off-thread; deck plays immediately, beat grid appears when ready.
        _ = Task.Run(async () =>
        {
            try
            {
                BasicAnalysis basic; string source;
                if (AnalysisProvider is not null)
                {
                    (basic, source) = await AnalysisProvider.GetAsync(filePath, stereoSamples, sampleRate);
                }
                else
                {
                    basic = await BasicAnalysis.ComputeAsync(filePath, stereoSamples, channels: 2, sampleRate: sampleRate, reporter: Reporter);
                    source = "computed";
                }
                Console.WriteLine($"[DeckPlayer] analysis from {source}: {basic.Bpm:F1} BPM, {basic.BeatTimes.Length} beats, {basic.DownbeatTimes.Length} downbeats");
                Analysis.Set(basic);
                AnalysisUpdated?.Invoke();
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[DeckPlayer] analysis failed: {ex.Message}");
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
        if (_stemPlayers is not null)
        {
            foreach (var p in _stemPlayers)
            {
                p.Stop();
                _deckMixer?.RemoveComponent(p);
                p.Dispose();
            }
            _stemPlayers = null;
        }
    }

    /// <summary>Swap the single-track player for four stem players that share the
    /// engine clock and start at the original's current play position.</summary>
    private void SwitchToStemMode(StemPaths stems)
    {
        if (_engine is null || _deckMixer is null) return;

        // Decode all 4 stems off the audio thread (we're already on a background task).
        var samples = new[]
        {
            AudioFileDecoder.Decode(stems.Drums),
            AudioFileDecoder.Decode(stems.Vocals),
            AudioFileDecoder.Decode(stems.Bass),
            AudioFileDecoder.Decode(stems.Other),
        };

        var posSeconds = _player?.Time ?? 0;
        var wasPlaying = IsPlaying;

        // Tear down original single player first.
        if (_player is not null)
        {
            _player.Stop();
            _deckMixer.RemoveComponent(_player);
            _player.Dispose();
            _player = null;
        }

        var players = new SoundPlayer[4];
        for (int i = 0; i < 4; i++)
        {
            var prov = new RawDataProvider(samples[i], 44100);
            var p = new SoundPlayer(_engine, _format, prov);
            // Single shared EQ in front of each stem — every stem sees the same isolator.
            if (_eq is not null) p.AddModifier(_eq);
            _deckMixer.AddComponent(p);
            p.Seek(TimeSpan.FromSeconds(Math.Max(0, posSeconds)));
            players[i] = p;
        }
        _stemPlayers = players;
        ApplyDeckGain();

        if (wasPlaying)
            foreach (var p in players) p.Play();

        Console.WriteLine("[DeckPlayer] switched to stem-mix playback");
    }

    /// <summary>Mute / unmute a stem group (0=Drums, 1=Vocals, 2=Instrumental=bass+other).
    /// Returns the new on/off state for the group.</summary>
    public bool ToggleStemGroup(int group)
    {
        if (_stemPlayers is null) return true;  // no stems yet — no-op, group stays "on"
        bool newState = group switch
        {
            0 => !_stemActive[STEM_DRUMS],
            1 => !_stemActive[STEM_VOCALS],
            _ => !(_stemActive[STEM_BASS] && _stemActive[STEM_OTHER]),
        };
        switch (group)
        {
            case 0: _stemActive[STEM_DRUMS]  = newState; break;
            case 1: _stemActive[STEM_VOCALS] = newState; break;
            default:
                _stemActive[STEM_BASS]  = newState;
                _stemActive[STEM_OTHER] = newState;
                break;
        }
        ApplyDeckGain();
        return newState;
    }

    /// <summary>Push the current Volume × per-stem-mute matrix onto the SoundPlayers.</summary>
    private void ApplyDeckGain()
    {
        if (_stemPlayers is not null)
        {
            for (int i = 0; i < _stemPlayers.Length; i++)
                _stemPlayers[i].Volume = _stemActive[i] ? _volume : 0f;
        }
        else if (_player is not null)
        {
            _player.Volume = _volume;
        }
    }

    /// <summary>Raised on the analysis thread once BasicAnalysis completes.</summary>
    public event Action? AnalysisUpdated;

    /// <summary>Eject the current track: stop playback, detach the SoundPlayer, clear analysis.</summary>
    public void Unload()
    {
        TearDownPlayers();
        _sampleCount = 0;
        Analysis = new TrackAnalysis();
        // Re-arm stem activity so the next track loads with everything on.
        for (int i = 0; i < _stemActive.Length; i++) _stemActive[i] = true;
        AnalysisUpdated?.Invoke();
    }

    public void Play()
    {
        foreach (var p in ActivePlayers) p.Play();
    }

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
    public void Pause()
    {
        foreach (var p in ActivePlayers) p.Pause();
    }

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
        if (_stemPlayers is not null)
        {
            var first = _stemPlayers[0];
            double target = Math.Clamp(first.Time + seconds, 0.0, first.Duration);
            foreach (var p in _stemPlayers) p.Seek(TimeSpan.FromSeconds(target));
            return;
        }
        if (_player is null) return;
        double mainTarget = Math.Clamp(_player.Time + seconds, 0.0, _player.Duration);
        _player.Seek(TimeSpan.FromSeconds(mainTarget));
    }

    /// <summary>
    /// Set one of the 3 EQ bands (0=Low, 1=Mid, 2=High). <paramref name="value"/> is 0..1,
    /// 0.5 = unity. Below 0.5 cuts down to −26 dB (full kill); above 0.5 boosts up to +6 dB.
    /// Safe to call from any thread — the audio thread sees the new gain on the next buffer.
    /// </summary>
    public void SetEq(int band, double value)
    {
        if (_eq is null && _engine is not null)
            _eq = new BiquadEq3Band(_engine, _format);  // allow pot moves before load

        // Isolator gain: 0 → mute, 0.5 → unity (1.0), 1 → +6 dB (2.0).
        // Curve is intentionally linear so the centre detent at v=0.5 reads as flat.
        double v = Math.Clamp(value, 0.0, 1.0);
        float gain = v < 0.5
            ? (float)(v * 2.0)
            : (float)(1.0 + (v - 0.5) * 2.0);
        _eq?.SetBandGain(band, gain);
    }
}
