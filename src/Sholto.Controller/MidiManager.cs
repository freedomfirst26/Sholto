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

    public event Action<ControllerEvent>? EventReceived;

    /// <summary>When true, log every incoming MIDI message to console — for mapping new controls.</summary>
    public bool LogAllMessages { get; set; }

    public bool Connect()
    {
        foreach (var mapping in MappingRegistry.All)
        {
            var raw = AlsaRawMidi.Open(mapping.DeviceNameMatch);
            if (raw is null) continue;

            _mapping = mapping;
            _rawMidi = raw;
            _rawMidi.MessageReceived += OnRawMidi;
            Console.WriteLine($"[MIDI] connected to {mapping.DeviceNameMatch} via /dev/snd raw MIDI (mapping: {mapping.GetType().Name})");
            return true;
        }
        return false;
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
        ControllerEvent? evt = type switch
        {
            0x90 when data2 > 0 => _mapping.Translate(new NoteEvent(channel, data1, data2)),
            0xB0                => _mapping.Translate(new CcEvent(channel, data1, data2)),
            _                   => null,
        };
        if (evt is not null) EventReceived?.Invoke(evt);
    }

    public void Dispose() => _rawMidi?.Dispose();
}
