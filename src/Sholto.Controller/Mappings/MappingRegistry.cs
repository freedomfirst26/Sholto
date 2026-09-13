namespace Sholto.Controller.Mappings;

/// <summary>
/// Single source of truth for which IControllerMapping handles which device.
/// Add new mappings here when you create them in this folder.
/// Composed once at bootstrap with the app's configured <see cref="DdjFlx4Options"/>
/// and handed to <see cref="MidiManager"/> — not a static registry, so the mapping
/// set is decided in one place rather than rebuilt (with whatever options happen to
/// be in scope) wherever a connection is attempted.
/// </summary>
public sealed class MappingRegistry : IControllerMappings
{
    /// <summary>The configured mapping set — one entry per supported device.</summary>
    public IReadOnlyList<IControllerMapping> Mappings { get; }

    public MappingRegistry(DdjFlx4Options flx4Options) => Mappings = new IControllerMapping[]
    {
        new DdjFlx4Mapping(flx4Options),
        // new SomeOtherControllerMapping(),
    };

    /// <summary>Find the mapping that matches a connected device by name (substring match).</summary>
    public IControllerMapping? FindForDevice(string deviceName)
    {
        foreach (var m in Mappings)
            if (deviceName.Contains(m.DeviceNameMatch, StringComparison.OrdinalIgnoreCase))
                return m;
        return null;
    }
}
