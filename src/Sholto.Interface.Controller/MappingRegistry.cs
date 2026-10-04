namespace Sholto.Interface.Controller;

/// <summary>
/// The set of device mappings this build supports, and the lookup that matches a
/// connected device to one of them. Composed once at bootstrap with the mappings
/// the composition root chose and handed to <see cref="MidiManager"/> — not a
/// static registry, so the mapping set is decided in one place rather than rebuilt
/// wherever a connection is attempted.
/// </summary>
public sealed class MappingRegistry(IEnumerable<IControllerMapping> mappings) : IControllerMappings
{
    /// <summary>The configured mapping set — one entry per supported device.</summary>
    public IReadOnlyList<IControllerMapping> Mappings { get; } = mappings.ToList();

    /// <summary>Find the mapping that matches a connected device by name (substring match).</summary>
    public IControllerMapping? FindForDevice(string deviceName)
    {
        foreach (var m in Mappings)
            if (deviceName.Contains(m.DeviceNameMatch, StringComparison.OrdinalIgnoreCase))
                return m;
        return null;
    }
}
