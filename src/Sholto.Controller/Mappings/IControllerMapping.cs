namespace Sholto.Controller.Mappings;

public readonly record struct NoteEvent(int Channel, int Key, int Velocity);
public readonly record struct CcEvent(int Channel, int Control, int Value);

/// <summary>
/// Translates raw MIDI messages from a specific hardware controller into
/// Sholto <see cref="ControllerEvent"/>s.
///
/// To add support for a new controller:
///   1. Create a class in this folder named after the device (e.g. <c>PioneerDdj400Mapping</c>).
///   2. Implement this interface — return the appropriate <see cref="ControllerEvent"/>
///      for each note or CC, or <c>null</c> to ignore.
///   3. Register your mapping in <see cref="MappingRegistry"/>.
///   4. Use <c>MidiManager.LogAllMessages = true</c> while you're figuring out the
///      controller's CC/note numbers — every byte gets dumped to the console.
///
/// Channel/Key/Control values are raw wire numbers (0–15 for channel, 0–127 for key/CC).
/// </summary>
public interface IControllerMapping
{
    /// <summary>Substring that identifies this controller in /proc/asound/cards.</summary>
    string DeviceNameMatch { get; }

    ControllerEvent? Translate(NoteEvent msg);
    ControllerEvent? Translate(CcEvent msg);
}
