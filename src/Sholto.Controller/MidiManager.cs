using Sholto.Controller.Mappings;

namespace Sholto.Controller;

/// <summary>
/// Reads MIDI bytes from /dev/snd/midiC<N>D0 (the ALSA raw-MIDI character device)
/// and dispatches them through the matching <see cref="IControllerMapping"/>.
///
/// Linux-only. Works regardless of whether ALSA is fronted by PipeWire, PulseAudio,
/// or running standalone — no audio-server bridges, just file I/O against the kernel's
/// raw-MIDI character device.
/// </summary>
public sealed class MidiManager : IDisposable
{
    private AlsaRawMidi? _rawMidi;
    private IControllerMapping? _mapping;
    private CancellationTokenSource? _superCts;

    public event Action<ControllerEvent>? EventReceived;

    /// <summary>Raised each time the controller (re)connects — after a cold start
    /// or after a mid-session drop is recovered. Lets the App re-assert LED state.</summary>
    public event Action? Connected;

    /// <summary>Raised when a live controller drops out. Reconnection is already
    /// in progress when this fires; it's purely informational (logging/UI).</summary>
    public event Action? ConnectionLost;

    /// <summary>True while a device is currently open.</summary>
    public bool IsConnected => _rawMidi is not null;

    /// <summary>When true, log every incoming MIDI message to console — for mapping new controls.</summary>
    public bool LogAllMessages { get; set; }

    /// <summary>The active device mapping, or null until <see cref="Connect"/>
    /// succeeds. Used by the lighting side to render logical lights to bytes.</summary>
    public IControllerMapping? Mapping => _mapping;

    /// <summary>Write raw MIDI bytes out to the controller (e.g. LED updates).</summary>
    public void Send(byte[] bytes) => _rawMidi?.SendRaw(bytes);

    /// <summary>Start keeping a controller connected. Makes one immediate attempt
    /// (so a controller present at launch is live at once) and then supervises in
    /// the background: if none is found, or a live one drops, it retries every
    /// couple of seconds until the app exits. Cheap — a poll is a directory read
    /// of /proc/asound/cards when nothing is plugged. Returns whether the first
    /// attempt connected.</summary>
    public bool Connect()
    {
        if (_superCts is not null) return IsConnected;   // already supervising
        _superCts = new CancellationTokenSource();
        bool first = TryConnectOnce();
        _ = SuperviseAsync(_superCts.Token);
        return first;
    }

    private bool TryConnectOnce()
    {
        foreach (var mapping in MappingRegistry.All)
        {
            var raw = AlsaRawMidi.Open(mapping.DeviceNameMatch);
            if (raw is null) continue;

            _mapping = mapping;
            raw.MessageReceived += OnRawMidi;
            raw.Disconnected += OnDeviceLost;
            _rawMidi = raw;
            Console.WriteLine($"[MIDI] connected to {mapping.DeviceNameMatch} via /dev/snd raw MIDI (mapping: {mapping.GetType().Name})");
            Connected?.Invoke();
            return true;
        }
        return false;
    }

    private void OnDeviceLost()
    {
        // Runs on the dying read-loop thread — never block it. Detach and dispose
        // off-thread; the supervisor will reconnect on its next tick.
        var old = _rawMidi;
        _rawMidi = null;
        Console.WriteLine("[MIDI] controller disconnected — retrying until it comes back");
        ConnectionLost?.Invoke();
        if (old is not null) _ = Task.Run(old.Dispose);
    }

    private async Task SuperviseAsync(CancellationToken ct)
    {
        while (!ct.IsCancellationRequested)
        {
            try { await Task.Delay(2000, ct); }
            catch (OperationCanceledException) { break; }
            if (!IsConnected) TryConnectOnce();
        }
    }

    private void OnRawMidi(byte status, byte data1, byte data2)
    {
        // We pass the 1-indexed MIDI channel through to mappings so the numbers
        // line up with what `LogAllMessages` prints (and what users read off the
        // back of the controller). Wire 0 → channel 1, wire 15 → channel 16.
        int channel = (status & 0x0F) + 1;
        int type = status & 0xF0;

        if (LogAllMessages)
        {
            string kind = type switch
            {
                0x80 => "NoteOff",
                0x90 => data2 > 0 ? "NoteOn" : "NoteOff",
                0xB0 => "CC",
                _    => $"0x{type:X2}",
            };
            Console.WriteLine($"[MIDI raw] ch={channel:00} {kind,-7} key/cc=0x{data1:X2}({data1,3}) val={data2,3}");
        }

        if (_mapping is null) return;
        // Pioneer (and most controllers) send NoteOn 0x90 with vel=0 as "release" instead of
        // a real 0x80 NoteOff. Treat both as note-up so long-press handling (e.g. browse-hold
        // → re-analyze) can see the release edge.
        ControllerEvent? evt = type switch
        {
            0x90                => _mapping.Translate(new NoteEvent(channel, data1, data2, IsDown: data2 > 0)),
            0x80                => _mapping.Translate(new NoteEvent(channel, data1, data2, IsDown: false)),
            0xB0                => _mapping.Translate(new CcEvent(channel, data1, data2)),
            _                   => null,
        };
        if (evt is not null) EventReceived?.Invoke(evt);
    }

    public void Dispose()
    {
        _superCts?.Cancel();
        _superCts?.Dispose();
        _rawMidi?.Dispose();
    }
}
