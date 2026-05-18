using RtMidi.Core;
using RtMidi.Core.Devices;
using RtMidi.Core.Messages;

namespace OpenDJ.Controller;

public sealed class MidiManager : IDisposable
{
    private IMidiInputDevice? _device;

    public event Action<ControllerEvent>? EventReceived;

    public bool Connect()
    {
        foreach (var info in MidiDeviceManager.Default.InputDevices)
        {
            if (!info.Name.Contains("DDJ-FLX4", StringComparison.OrdinalIgnoreCase))
                continue;

            _device = info.CreateDevice();
            _device.NoteOn += OnNoteOn;
            _device.ControlChange += OnControlChange;
            _device.Open();
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

    public void Dispose()
    {
        _device?.Close();
        _device?.Dispose();
    }
}
