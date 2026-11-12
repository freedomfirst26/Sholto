namespace Sholto.Interface.Controller.WinMm;

/// <summary>The WinMM MIDI device list and the way to open one entry of it. The thin seam over NAudio.Midi's
/// statics (<see cref="NAudioWinMmDevices"/> is the real one) so <see cref="WinMmMidiPortOpener"/> and
/// <see cref="WinMmMidiPort"/> are testable without winmm.dll. Opening throws NAudio's own
/// <c>NAudio.MmException</c> on failure.</summary>
public interface IWinMmDevices
{
    /// <summary>The product name of every MIDI input, in device-index order.</summary>
    IReadOnlyList<string> InputNames();

    /// <summary>The product name of every MIDI output, in device-index order.</summary>
    IReadOnlyList<string> OutputNames();

    /// <summary>Open the input at <paramref name="index"/>; it delivers nothing until started.</summary>
    IWinMmIn OpenIn(int index);

    /// <summary>Open the output at <paramref name="index"/>.</summary>
    IWinMmOut OpenOut(int index);
}
