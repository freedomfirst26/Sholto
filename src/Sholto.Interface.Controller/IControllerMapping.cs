using Sholto.Data;

namespace Sholto.Interface.Controller;

/// <summary>
/// Translates raw MIDI messages from a specific hardware controller into
/// Sholto <see cref="ControllerEvent"/>s.
///
/// To add support for a new controller:
///   1. Add a folder in Sholto.Interface.Controller.Mappings named after the device, with a class
///      named after the device (e.g. <c>PioneerDdj400Mapping</c>).
///   2. Implement this interface — return the appropriate <see cref="ControllerEvent"/>
///      for each note or CC, or <c>null</c> to ignore.
///   3. Add an entry for your mapping in <c>ControllerMappingCatalog</c> (Sholto.Interface.Controller.Mappings).
///   4. Use <c>MidiManager.LogAllMessages = true</c> while you're figuring out the
///      controller's CC/note numbers — every byte gets dumped to the console.
///
/// Channel/Key/Control values are raw wire numbers (0–15 for channel, 0–127 for key/CC).
/// </summary>
public interface IControllerMapping
{
    /// <summary>Substring that identifies this controller in /proc/asound/cards.</summary>
    string DeviceNameMatch { get; }

    /// <summary>Substrings that identify this controller's built-in sound card in
    /// playback-device and PipeWire sink names. Empty if it has none.</summary>
    IReadOnlyList<string> AudioDeviceNameMatches { get; }

    ControllerEvent? Translate(NoteEvent msg);
    ControllerEvent? Translate(CcEvent msg);

    /// <summary>Render a logical light to the raw MIDI bytes that set it on this
    /// device, or <c>null</c> if the device has no such light. Default: no lights.
    /// This is the output counterpart of <see cref="Translate(NoteEvent)"/>.</summary>
    byte[]? RenderLight(ControllerLight light, bool on) => null;

    /// <summary>The raw MIDI bytes to send once, immediately after a successful
    /// connection, to put the device into the state this mapping expects (e.g.
    /// selecting a pad mode) — or <c>null</c> if the device needs no such
    /// initialisation. The transport writes these bytes verbatim; it does not
    /// interpret them. Default: no startup init.</summary>
    byte[]? StartupInit() => null;

    /// <summary>The raw MIDI bytes that make a device which holds its own pad mode switch a deck to
    /// <paramref name="page"/> (e.g. a simulated press of the mode button), or <c>null</c> if the device
    /// cannot be told or has no such page. Default: none.</summary>
    byte[]? RenderPadMode(int deck, PadPage page) => null;
}
