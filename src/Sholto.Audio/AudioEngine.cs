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
    // MASTER CUE state, kept here so it survives a device switch (which builds a
    // fresh router) — re-applied to the new router in Start.
    private bool _masterCueActive;
    // Set while the FLX4 is open AND its master bus has been PipeWire-routed
    // to a separate speaker sink (see Start/ApplyPipeWireMasterRoute). Used by
    // Stop() to relink master back onto the FLX4 before the device closes.
    private string? _routedFlx4Node;

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

    public void Start() => Start(masterSpeakerName: null);

    /// <summary>
    /// Opens the playback device and starts the graph.
    /// <paramref name="masterSpeakerName"/> is the user's chosen MASTER
    /// SPEAKER (the "Choose Audio Output" picker in the UI excludes the
    /// FLX4 — see App.axaml.cs) — not necessarily the device that gets
    /// opened:
    /// <list type="bullet">
    /// <item>If the DDJ-FLX4 is connected, it is ALWAYS the device miniaudio
    /// opens (4ch: master 1-2 + headphone cue 3-4 — it's also the shared
    /// clock for cue). <paramref name="masterSpeakerName"/> is then applied
    /// as a PipeWire re-route of just the master FL/FR ports onto that
    /// speaker sink, via <see cref="PipeWireRouter"/>; RL/RR (cue) stay on
    /// the FLX4. If routing isn't possible (tool missing, sink not found,
    /// not on Linux/PipeWire) this degrades to master+cue both on the FLX4 —
    /// today's behaviour.</item>
    /// <item>If the FLX4 is NOT connected, <paramref name="masterSpeakerName"/>
    /// is opened directly (2ch, no cue bus) — no PipeWire routing involved.</item>
    /// </list>
    /// </summary>
    public void Start(string? masterSpeakerName)
    {
        var flx4 = ResolveFlx4();
        bool haveFlx4 = flx4.Name is not null;
        var target = haveFlx4 ? flx4 : Resolve(masterSpeakerName);
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
        _router = new CueOutputRouter(_engine, deviceFormat, _decks) { MasterCueActive = _masterCueActive };
        _playbackDevice.MasterMixer.AddComponent(_router);
        _playbackDevice.Start();
        _running = true;
        string mode = channels >= 4 ? "master 1-2 + headphone cue 3-4" : "stereo master (no cue bus)";
        Console.WriteLine($"[AudioEngine] device={target.Name} started; {channels}ch — {mode}; buffer={cfg.PeriodSizeInMilliseconds}ms × {cfg.Periods}");

        // FLX4 open + a distinct master speaker chosen → re-link master FL/FR
        // onto that speaker via PipeWire (best-effort; see PipeWireRouter).
        if (haveFlx4 && masterSpeakerName is not null && !PipeWireRouter.IsFlx4(masterSpeakerName))
            ApplyPipeWireMasterRoute(masterSpeakerName);
    }

    /// <summary>Best-effort: finds the FLX4's and the chosen speaker's PipeWire
    /// sink node names and asks PipeWireRouter to relink master FL/FR between
    /// them. Any failure just logs and leaves master on the FLX4 — never
    /// throws, never blocks longer than PipeWireRouter's own ~3s port poll.</summary>
    private void ApplyPipeWireMasterRoute(string masterSpeakerName)
    {
        if (!PipeWireRouter.IsAvailable())
        {
            Console.WriteLine("[AudioEngine] pw-link not available — master stays on FLX4 (not on PipeWire?)");
            return;
        }
        var flx4Node = PipeWireRouter.FindFlx4Sink();
        if (flx4Node is null)
        {
            Console.WriteLine("[AudioEngine] FLX4 not found via pactl — master stays on FLX4");
            return;
        }
        var speaker = PipeWireRouter.EnumerateSpeakerSinks().FirstOrDefault(s => s.Desc == masterSpeakerName);
        if (speaker.Node is null)
        {
            Console.WriteLine($"[AudioEngine] master speaker '{masterSpeakerName}' not found among PipeWire sinks — master stays on FLX4");
            return;
        }
        if (PipeWireRouter.ApplyMasterRoute(flx4Node, speaker.Node, out var log))
        {
            _routedFlx4Node = flx4Node;
            Console.WriteLine($"[AudioEngine] master routed to '{masterSpeakerName}': {log}");
        }
        else
        {
            Console.WriteLine($"[AudioEngine] master route to '{masterSpeakerName}' failed: {log}");
        }
    }

    public void SwitchDevice(string masterSpeakerName)
    {
        // Rebuild the graph so the new device's channel count (and therefore cue
        // availability) takes effect — a 2ch device has no ch3-4. Start() below
        // re-applies (or skips) the PipeWire master route as appropriate.
        Stop();
        Start(masterSpeakerName);
    }

    /// <summary>MASTER CUE toggle — fold the master mix into the headphone cue
    /// (ch3-4) so you can monitor the speakers' output in your phones. Remembered
    /// across a device switch (re-applied to the fresh router in Start).</summary>
    public void SetMasterCue(bool on)
    {
        _masterCueActive = on;
        if (_router is not null) _router.MasterCueActive = on;
        Console.WriteLine($"[AudioEngine] master cue {(on ? "ON" : "off")}");
    }

    /// <summary>Finds the FLX4 among playback devices (matched by name — see
    /// <see cref="PipeWireRouter.IsFlx4"/> — not a fixed device name, since the
    /// node name embeds the unit's serial number). Returned DeviceInfo has a
    /// null Name if no FLX4 is connected, matching the FirstOrDefault-on-struct
    /// pattern used by <see cref="Resolve"/> below.</summary>
    private DeviceInfo ResolveFlx4()
    {
        _engine.UpdateAudioDevicesInfo();
        return _engine.PlaybackDevices.FirstOrDefault(d => PipeWireRouter.IsFlx4(d.Name));
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
        // Undo any PipeWire master re-route before the device (and its ports)
        // go away, so a stale link isn't left dangling on the speaker sink.
        if (_routedFlx4Node is not null)
        {
            PipeWireRouter.ResetMasterRoute(_routedFlx4Node);
            _routedFlx4Node = null;
        }
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
