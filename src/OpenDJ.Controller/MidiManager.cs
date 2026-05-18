using RtMidi.Core;
using RtMidi.Core.Devices;
using RtMidi.Core.Enums;
using RtMidi.Core.Messages;

namespace OpenDJ.Controller;

public sealed class MidiManager : IDisposable
{
    private IMidiInputDevice? _rtDevice;
    private AlsaRawMidi? _rawMidi;

    public event Action<ControllerEvent>? EventReceived;

    public bool Connect()
    {
        // 1. Try RtMidi.Core (works on Windows / Mac and ALSA-direct Linux setups).
        var inputs = MidiDeviceManager.Default.InputDevices.ToList();
        Console.WriteLine($"[MIDI] RtMidi sees {inputs.Count} input device(s):");
        foreach (var info in inputs)
            Console.WriteLine($"[MIDI]   - '{info.Name}'");

        foreach (var info in inputs)
        {
            if (!info.Name.Contains("DDJ-FLX4", StringComparison.OrdinalIgnoreCase))
                continue;

            _rtDevice = info.CreateDevice();
            _rtDevice.NoteOn += OnNoteOn;
            _rtDevice.ControlChange += OnControlChange;
            _rtDevice.Open();
            Console.WriteLine($"[MIDI] connected via RtMidi to '{info.Name}'");
            return true;
        }

        // 2. Linux fallback: RtMidi can silently fail under PipeWire; read raw MIDI
        //    directly from /dev/snd/midiC<card>D0.
        _rawMidi = AlsaRawMidi.Open("DDJ-FLX4");
        if (_rawMidi is not null)
        {
            _rawMidi.MessageReceived += OnRawMidi;
            Console.WriteLine("[MIDI] connected via /dev/snd raw fallback to DDJ-FLX4");
            return true;
        }

        return false;
    }

    private void OnNoteOn(IMidiInputDevice sender, in NoteOnMessage msg)
    {
        var evt = DdjFlx4Mapping.Translate(msg);
        if (evt is not null) EventReceived?.Invoke(evt);
    }

    private void OnControlChange(IMidiInputDevice sender, in ControlChangeMessage msg)
    {
        var evt = DdjFlx4Mapping.Translate(msg);
        if (evt is not null) EventReceived?.Invoke(evt);
    }

    /// <summary>When true, log every incoming MIDI message to console — for mapping new controls.</summary>
    public bool LogAllMessages { get; set; }

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

        ControllerEvent? evt = type switch
        {
            0x90 when data2 > 0 => DdjFlx4Mapping.Translate(
                new NoteOnMessage((Channel)channel, (Key)data1, data2)),
            0xB0 => DdjFlx4Mapping.Translate(
                new ControlChangeMessage((Channel)channel, data1, data2)),
            _ => null
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
