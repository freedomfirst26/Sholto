namespace Sholto.Interface.Controller;

/// <summary>Makes the <see cref="IMidiPortOpener"/> for an operating system, so the composition root picks the
/// MIDI transport by OS without naming the concrete openers.</summary>
public interface IMidiPortOpenerFactory
{
    /// <summary>The WinMM (NAudio.Midi) opener.</summary>
    IMidiPortOpener Windows();

    /// <summary>The raw ALSA opener.</summary>
    IMidiPortOpener Linux();

    /// <summary>The opener for the OS this process runs on: <see cref="Windows"/> on Windows, else <see cref="Linux"/>.</summary>
    IMidiPortOpener Create();
}
