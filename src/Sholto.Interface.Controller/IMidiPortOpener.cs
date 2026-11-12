namespace Sholto.Interface.Controller;

/// <summary>Finds and opens the MIDI port for a named controller. Taken by
/// <see cref="MidiManager"/> so the OS-specific device lookup is a collaborator
/// handed in at bootstrap, not a static reached by name.</summary>
public interface IMidiPortOpener
{
    /// <summary>Open the first device whose name contains <paramref name="deviceName"/>
    /// (case-insensitive), or null when this OS is not supported or no such device
    /// exists. A new implementation should log and return null when opening a
    /// matching device fails, rather than throw (the supervisor retries).</summary>
    IRawMidiPort? Open(string deviceName);
}
