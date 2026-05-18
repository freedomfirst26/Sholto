using OpenDJ.Controller.Mappings;
using RtMidi.Core;
using RtMidi.Core.Devices;
using RtMidi.Core.Enums;
using RtMidi.Core.Messages;

namespace OpenDJ.Controller;

public sealed class MidiManager : IDisposable
{
    private IMidiInputDevice? _rtDevice;
    private AlsaRawMidi? _rawMidi;
    private IControllerMapping? _mapping;

    public event Action<ControllerEvent>? EventReceived;

    /// <summary>When true, log every incoming MIDI message to console — for mapping new controls.</summary>
    public bool LogAllMessages { get; set; }

    public bool Connect()
    {
        // 1. Try RtMidi.Core (works on Windows / Mac and ALSA-direct Linux setups).
        var inputs = MidiDeviceManager.Default.InputDevices.ToList();
        Console.WriteLine($"[MIDI] RtMidi sees {inputs.Count} input device(s):");
        foreach (var info in inputs)
            Console.WriteLine($"[MIDI]   - '{info.Name}'");

        foreach (var info in inputs)
        {
            var match = MappingRegistry.FindForDevice(info.Name);
            if (match is null) continue;

            _mapping = match;
            _rtDevice = info.CreateDevice();
            _rtDevice.NoteOn += OnNoteOn;
            _rtDevice.ControlChange += OnControlChange;
            _rtDevice.Open();
            Console.WriteLine($"[MIDI] connected via RtMidi to '{info.Name}' (mapping: {_mapping.GetType().Name})");
            return true;
        }

        // 2. Linux fallback: RtMidi can silently fail under PipeWire; read raw MIDI
        //    directly from /dev/snd/midiC<card>D0. Probe each registered mapping
        //    by its DeviceNameMatch.
        foreach (var mapping in MappingRegistry.All)
        {
            var raw = AlsaRawMidi.Open(mapping.DeviceNameMatch);
            if (raw is null) continue;

            _mapping = mapping;
            _rawMidi = raw;
            _rawMidi.MessageReceived += OnRawMidi;
            Console.WriteLine($"[MIDI] connected via /dev/snd raw fallback to {mapping.DeviceNameMatch} (mapping: {mapping.GetType().Name})");
            return true;
        }

        return false;
    }

    private void OnNoteOn(IMidiInputDevice sender, in NoteOnMessage msg)
    {
        var evt = _mapping?.Translate(msg);
        if (evt is not null) EventReceived?.Invoke(evt);
    }

    private void OnControlChange(IMidiInputDevice sender, in ControlChangeMessage msg)
    {
        var evt = _mapping?.Translate(msg);
        if (evt is not null) EventReceived?.Invoke(evt);
    }

    private void OnRawMidi(byte status, byte data1, byte data2)
    {
        int channel = status & 0x0F;
        int type = status & 0xF0;

        if (LogAllMessages)
        {
            string kind = type switch
            {
                0x80 => "NoteOff",
                0x90 => data2 > 0 ? "NoteOn" : "NoteOff",
                0xB0 => "CC",
                _    => $"0x{type:X2}"
            };
            Console.WriteLine($"[MIDI raw] ch={channel + 1:00} {kind,-7} key/cc=0x{data1:X2}({data1,3}) val={data2,3}");
        }

        if (_mapping is null) return;
        ControllerEvent? evt = type switch
        {
            0x90 when data2 > 0 => _mapping.Translate(new NoteOnMessage((Channel)channel, (Key)data1, data2)),
            0xB0                => _mapping.Translate(new ControlChangeMessage((Channel)channel, data1, data2)),
            _                   => null,
        };
        if (evt is not null) EventReceived?.Invoke(evt);
    }

    public void Dispose()
    {
        _rtDevice?.Close();
        _rtDevice?.Dispose();
        _rawMidi?.Dispose();
    }
}
