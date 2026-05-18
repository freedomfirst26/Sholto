using SoundFlow.Abstracts.Devices;
using SoundFlow.Backends.MiniAudio;
using SoundFlow.Backends.MiniAudio.Enums;
using SoundFlow.Enums;
using SoundFlow.Structs;
using SfEngine = SoundFlow.Abstracts.AudioEngine;

namespace OpenDJ.Audio;

/// <summary>
/// Wraps a SoundFlow MiniAudioEngine + one playback device. The DeckPlayer's
/// SoundFlow SoundPlayer is attached to the device's MasterMixer.
/// </summary>
public sealed class AudioEngine : IAudioOutput
{
    public static readonly AudioFormat Format = new()
    {
        SampleRate = 48000,
        Channels = 2,
        Format = SampleFormat.F32
    };

    private readonly DeckPlayer _deckA;
    private readonly SfEngine _engine;
    private AudioPlaybackDevice? _playbackDevice;
    private bool _running;

    public bool IsRunning => _running;
    public SfEngine Engine => _engine;

    public AudioEngine(DeckPlayer deckA)
    {
        _deckA = deckA;
        // Default backend selection — reports as "Oss" but actually routes via
        // PulseAudio on Linux. Forcing PulseAudio explicitly flips enumeration
        // to ALSA hardware names AND breaks audio (SoundFlow quirk on PipeWire).
        var miniEngine = new MiniAudioEngine();
        _engine = miniEngine;
        Console.WriteLine($"[AudioEngine] active backend: {miniEngine.ActiveBackend}");
        _deckA.AttachEngine(_engine, Format);
    }

    public void Start() => Start(deviceName: null);

    public void Start(string? deviceName)
    {
        var target = Resolve(deviceName);
        _playbackDevice = _engine.InitializePlaybackDevice(target, Format);
        _playbackDevice.MasterMixer.AddComponent(_deckA.Component);
        _playbackDevice.Start();
        _running = true;
        Console.WriteLine($"[AudioEngine] device={target.Name} started; deck component attached to master mixer");
    }

    public void SwitchDevice(string deviceName)
    {
        var target = Resolve(deviceName);
        if (_playbackDevice is null) { Start(deviceName); return; }
        _playbackDevice = _engine.SwitchDevice(_playbackDevice, target);
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
            _playbackDevice.MasterMixer.RemoveComponent(_deckA.Component);
            _playbackDevice.Dispose();
            _playbackDevice = null;
        }
        _running = false;
    }

    public void Dispose()
    {
        Stop();
        _engine.Dispose();
    }
}
