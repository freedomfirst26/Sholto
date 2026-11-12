using NAudio.Midi;

namespace Sholto.Interface.Controller.WinMm;

/// <summary>The real <see cref="IWinMmDevices"/>: NAudio.Midi over winmm.dll. Windows only; left untested (no
/// winmm.dll on the Linux test runner).</summary>
public sealed class NAudioWinMmDevices : IWinMmDevices
{
    public IReadOnlyList<string> InputNames() =>
        Enumerable.Range(0, MidiIn.NumberOfDevices).Select(i => MidiIn.DeviceInfo(i).ProductName).ToList();

    public IReadOnlyList<string> OutputNames() =>
        Enumerable.Range(0, MidiOut.NumberOfDevices).Select(i => MidiOut.DeviceInfo(i).ProductName).ToList();

    public IWinMmIn OpenIn(int index) => new NAudioWinMmIn(new MidiIn(index));

    public IWinMmOut OpenOut(int index) => new NAudioWinMmOut(new MidiOut(index));
}
