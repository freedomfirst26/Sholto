namespace Sholto.Controller.Mappings;

/// <summary>
/// Single source of truth for which IControllerMapping handles which device.
/// Add new mappings here when you create them in this folder.
/// </summary>
public static class MappingRegistry
{
    /// <summary>Default set of mappings (each device's options at their built-in
    /// defaults). Used where no configured options are available.</summary>
    public static readonly IReadOnlyList<IControllerMapping> All = Build(new DdjFlx4Options());

    /// <summary>Build the mapping set with a specific FLX-4 options instance —
    /// used by MidiManager so a configured DdjFlx4Options actually reaches the
    /// mapping it addresses.</summary>
    public static IReadOnlyList<IControllerMapping> Build(DdjFlx4Options flx4Options) => new IControllerMapping[]
    {
        new DdjFlx4Mapping(flx4Options),
        // new SomeOtherControllerMapping(),
    };

    /// <summary>Find the mapping that matches a connected device by name (substring match).</summary>
    public static IControllerMapping? FindForDevice(string deviceName)
    {
        foreach (var m in All)
            if (deviceName.Contains(m.DeviceNameMatch, StringComparison.OrdinalIgnoreCase))
                return m;
        return null;
    }
}
