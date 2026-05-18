using OpenDJ.Audio.Analysis;
using SoundFlow.Abstracts;
using SoundFlow.Components;
using SoundFlow.Enums;
using SoundFlow.Providers;
using SoundFlow.Structs;
using SfEngine = SoundFlow.Abstracts.AudioEngine;

namespace OpenDJ.Audio;

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

    public TrackAnalysis Analysis { get; private set; } = new();

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

    public void Load(float[] stereoSamples, int sampleRate)
    {
        if (_engine is null || _deckMixer is null)
            throw new InvalidOperationException("AttachEngine must be called first.");

        Analysis = new TrackAnalysis();
        Analysis.Set(BasicAnalysis.Compute(stereoSamples, channels: 2, sampleRate: sampleRate));
        _sampleRate = sampleRate;
        _sampleCount = stereoSamples.Length / 2;

        if (_player is not null)
        {
            _deckMixer.RemoveComponent(_player);
            _player.Dispose();
        }

        var provider = new RawDataProvider(stereoSamples, sampleRate);
        _player = new SoundPlayer(_engine, _format, provider);
        _deckMixer.AddComponent(_player);
        Console.WriteLine($"[DeckPlayer] loaded {stereoSamples.Length} samples @ {sampleRate}Hz; engine={_format.SampleRate}Hz {_format.Channels}ch {_format.Format}");
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

    /// <summary>Seek relative to current position by +/- seconds, clamped to track bounds.</summary>
    public void SeekRelative(double seconds)
    {
        if (_player is null) return;
        double target = Math.Clamp(_player.Time + seconds, 0.0, _player.Duration);
        _player.Seek(TimeSpan.FromSeconds(target));
    }
}
