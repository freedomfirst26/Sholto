using Microsoft.Extensions.Options;
using Sholto.Data;
using Sholto.Interface.Controller.WinMm;

namespace Sholto.Interface.Controller;

/// <summary>The real <see cref="IMidiPortOpenerFactory"/>.</summary>
public sealed class MidiPortOpenerFactory(IOptions<WinMmMidiOptions> winMm, TimeProvider time, ILogFactory logs) : IMidiPortOpenerFactory
{
    private readonly IOptions<WinMmMidiOptions> _winMm = winMm;
    private readonly TimeProvider _time = time;
    private readonly ILogFactory _logs = logs;

    public IMidiPortOpener Windows() => new WinMmMidiPortOpener(new NAudioWinMmDevices(), _winMm, _time, _logs);

    public IMidiPortOpener Linux() => new AlsaRawMidiOpener(_logs);

    public IMidiPortOpener Create() => OperatingSystem.IsWindows() ? Windows() : Linux();
}
