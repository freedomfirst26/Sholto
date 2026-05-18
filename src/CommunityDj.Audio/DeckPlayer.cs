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

    public TrackAnalysis Analysis { get; private set; } = new();

    private BiquadEq3Band? _eq;

    private float _volume = 1.0f;
    /// <summary>Linear gain [0..1]. Applied to the SoundPlayer so the deck's output is scaled before the master mixer sums it with the other deck.</summary>
    public float Volume
    {
        get => _volume;
        set
        {
            _volume = Math.Clamp(value, 0f, 1f);
            if (_player is not null) _player.Volume = _volume;
            Console.WriteLine($"[DeckPlayer] Volume = {_volume:F2}");
        }
    }

    public bool IsLoaded => _player is not null;
    public bool IsPlaying => _player?.State == PlaybackState.Playing;

    // Read provider.Position (raw samples consumed) directly — SoundPlayer.Time
    // converts using the engine sample rate, which drifts when the source rate
    // (44.1 kHz) differs from the engine rate (48 kHz).
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

        if (_player is not null)
        {
            _deckMixer.RemoveComponent(_player);
            _player.Dispose();
        }

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
                    basic = await BasicAnalysis.ComputeAsync(filePath, stereoSamples, channels: 2, sampleRate: sampleRate);
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
    }

    /// <summary>Raised on the analysis thread once BasicAnalysis completes.</summary>
    public event Action? AnalysisUpdated;

    /// <summary>Eject the current track: stop playback, detach the SoundPlayer, clear analysis.</summary>
    public void Unload()
    {
        if (_player is not null)
        {
            _player.Stop();
            _deckMixer?.RemoveComponent(_player);
            _player.Dispose();
            _player = null;
        }
        _sampleCount = 0;
        Analysis = new TrackAnalysis();
        AnalysisUpdated?.Invoke();
    }

    public void Play()
    {
        _player?.Play();
        Console.WriteLine($"[DeckPlayer] Play() → state={_player?.State}");
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
    public void Pause() => _player?.Pause();

    public void TogglePlay()
    {
        if (_player is null) { Console.WriteLine("[DeckPlayer] TogglePlay but no track loaded"); return; }
        if (_player.State == PlaybackState.Playing) _player.Pause();
        else _player.Play();
        Console.WriteLine($"[DeckPlayer] TogglePlay → state={_player.State}");
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
