namespace Sholto.Controller.Mappings;

/// <summary>
/// The set of device mappings this build supports, and the lookup that matches a
/// connected device to one of them.
///
/// A port rather than the concrete <see cref="MappingRegistry"/> so a caller can be
/// given a known mapping set — a single fake device, or none at all — without
/// constructing real mappings and their options. <see cref="MidiManager"/> is the
/// consumer, which is why this lives beside it.
/// </summary>
public interface IControllerMappings
{
    /// <summary>The configured mapping set — one entry per supported device.</summary>
    IReadOnlyList<IControllerMapping> Mappings { get; }

    /// <summary>Find the mapping that matches a connected device by name (substring
    /// match), or null if no supported device matches.</summary>
    IControllerMapping? FindForDevice(string deviceName);
}
