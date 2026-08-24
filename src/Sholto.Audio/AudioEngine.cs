using SoundFlow.Abstracts.Devices;
using SoundFlow.Backends.MiniAudio;
using SoundFlow.Backends.MiniAudio.Devices;
using SoundFlow.Backends.MiniAudio.Enums;
using SoundFlow.Enums;
using SoundFlow.Structs;
using SfEngine = SoundFlow.Abstracts.AudioEngine;

namespace Sholto.Audio;

/// <summary>
/// Wraps a SoundFlow MiniAudioEngine + one playback device. Every attached
/// <see cref="Deck"/>'s component is added to the device's MasterMixer
/// so their audio is summed at the output.
/// </summary>
public sealed class AudioEngine : IAudioOutput
{
    /// <summary>Decks always render internally in stereo. The device may be
    /// opened with more channels (see <see cref="Start(string)"/>) — the cue
    /// mix lands on channels 3-4 there — but each deck's own chain is 2ch.</summary>
    public static readonly AudioFormat DeckFormat = new()
    {
        SampleRate = 48000,
        Channels = 2,
        Format = SampleFormat.F32
    };

    private readonly IReadOnlyList<Deck> _decks;
    private readonly SfEngine _engine;
    private AudioPlaybackDevice? _playbackDevice;
    private CueOutputRouter? _router;
    private bool _running;

    public bool IsRunning => _running;
    public SfEngine Engine => _engine;

    public AudioEngine(params Deck[] decks)
    {
        _decks = decks;
        var miniEngine = new MiniAudioEngine();
        _engine = miniEngine;
        AudioFileDecoder.SoundFlowEngine = miniEngine;   // FLAC decode uses miniaudio via this engine
        Console.WriteLine($"[AudioEngine] active backend: {miniEngine.ActiveBackend}; decks={decks.Length}");
        foreach (var deck in _decks) deck.AttachEngine(_engine, DeckFormat);
    }

    /// <summary>4 if the device exposes ≥4 output channels (e.g. DDJ-FLX4:
    /// master 1-2 + headphone cue 3-4), otherwise 2 (stereo master only).</summary>
    private static int DeviceOutputChannels(DeviceInfo d)
    {
        int max = 2;
        var fmts = d.SupportedDataFormats;
        if (fmts is not null)
            foreach (var f in fmts)
                if ((int)f.Channels > max) max = (int)f.Channels;
        return max >= 4 ? 4 : 2;
    }

    public void Start() => Start(deviceName: null);

    public void Start(string? deviceName)
    {
        var target = Resolve(deviceName);
        int channels = DeviceOutputChannels(target);
        var deviceFormat = new AudioFormat
        {
            SampleRate = 48000,
            Channels = channels,
            Format = SampleFormat.F32,
        };

        // Generous buffer so an occasional GC pause or context switch doesn't
        // underrun the audio device. 20 ms × 3 periods = 60 ms total — still
        // tight enough for DJ latency, headroom for everything else.
        // (miniaudio's default on Linux/PulseAudio leans tighter than this and
        // glitches with even small managed-thread pauses.)
        var cfg = new MiniAudioDeviceConfig
        {
            PeriodSizeInMilliseconds = 20,
            Periods = 3,
        };

        _playbackDevice = _engine.InitializePlaybackDevice(target, deviceFormat, cfg);
        // One router is the device's source: it pulls each deck's post-EQ stereo
        // and composes master (ch1-2) + PFL cue (ch3-4). Decks are NOT added to
        // the mixer directly (that would sum them to stereo and double-process).
        _router = new CueOutputRouter(_engine, deviceFormat, _decks);
        _playbackDevice.MasterMixer.AddComponent(_router);
        _playbackDevice.Start();
        _running = true;
        string mode = channels >= 4 ? "master 1-2 + headphone cue 3-4" : "stereo master (no cue bus)";
        Console.WriteLine($"[AudioEngine] device={target.Name} started; {channels}ch — {mode}; buffer={cfg.PeriodSizeInMilliseconds}ms × {cfg.Periods}");
    }

    public void SwitchDevice(string deviceName)
    {
        // Rebuild the graph so the new device's channel count (and therefore cue
        // availability) takes effect — a 2ch device has no ch3-4.
        Stop();
        Start(deviceName);
    }

    private DeviceInfo Resolve(string? deviceName)
    {
        _engine.UpdateAudioDevicesInfo();
        if (deviceName is not null)
        {
            var match = _engine.PlaybackDevices.FirstOrDefault(d => d.Name == deviceName);
            if (match.Name is not null) return match;
            Console.WriteLine($"[AudioEngine] device '{deviceName}' not found; using default");
        }
        var def = _engine.PlaybackDevices.FirstOrDefault(d => d.IsDefault);
        if (def.Name is null)
            throw new InvalidOperationException("No playback devices found.");
        return def;
    }

    public void Stop()
    {
        if (_playbackDevice is not null)
        {
            if (_router is not null) _playbackDevice.MasterMixer.RemoveComponent(_router);
            _playbackDevice.Dispose();
            _playbackDevice = null;
            _router = null;
        }
        _running = false;
    }

    public void Dispose()
    {
        Stop();
        _engine.Dispose();
    }
}
