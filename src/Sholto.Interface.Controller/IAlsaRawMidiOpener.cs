namespace Sholto.Interface.Controller;

/// <summary>Finds and opens the ALSA raw-MIDI device for a named controller. Taken
/// by <see cref="MidiManager"/> so the /proc/asound read is a collaborator handed
/// in at bootstrap, not a static reached by name.</summary>
public interface IAlsaRawMidiOpener
{
    /// <summary>Open the first card whose name contains <paramref name="deviceName"/>
    /// (case-insensitive), or null when not on Linux or no such card has a raw-MIDI
    /// node.</summary>
    AlsaRawMidi? Open(string deviceName);
}
