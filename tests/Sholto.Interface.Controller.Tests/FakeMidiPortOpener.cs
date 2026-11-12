using Sholto.Interface.Controller;

namespace Sholto.Interface.Controller.Tests;

/// <summary>An <see cref="IMidiPortOpener"/> that hands back the port it was built with (null = no device)
/// and records each device name asked for.</summary>
public sealed class FakeMidiPortOpener(IRawMidiPort? port) : IMidiPortOpener
{
    private readonly IRawMidiPort? _port = port;

    public List<string> Requested { get; } = [];

    public IRawMidiPort? Open(string deviceName)
    {
        Requested.Add(deviceName);
        return _port;
    }
}
