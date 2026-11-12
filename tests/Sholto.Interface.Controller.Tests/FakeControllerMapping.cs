using Sholto.Interface.Controller;

namespace Sholto.Interface.Controller.Tests;

/// <summary>An <see cref="IControllerMapping"/> that records every message handed to Translate,
/// answers each with a fixed event, and returns a fixed <see cref="StartupInit"/>.</summary>
public sealed class FakeControllerMapping(string deviceNameMatch, byte[]? startupInit) : IControllerMapping
{
    private readonly byte[]? _startupInit = startupInit;

    public string DeviceNameMatch { get; } = deviceNameMatch;
    public IReadOnlyList<string> AudioDeviceNameMatches { get; } = [];

    public List<NoteEvent> Notes { get; } = [];
    public List<CcEvent> Ccs { get; } = [];

    public ControllerEvent? Translate(NoteEvent msg)
    {
        Notes.Add(msg);
        return new ControllerEvent.PlayPressed(0);
    }

    public ControllerEvent? Translate(CcEvent msg)
    {
        Ccs.Add(msg);
        return null;
    }

    public byte[]? StartupInit() => _startupInit;
}
