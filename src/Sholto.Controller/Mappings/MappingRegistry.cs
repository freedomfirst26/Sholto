namespace Sholto.Controller.Mappings;

/// <summary>
/// Single source of truth for which IControllerMapping handles which device.
/// Add new mappings here when you create them in this folder.
/// </summary>
public static class MappingRegistry
{
    public static readonly IReadOnlyList<IControllerMapping> All = new IControllerMapping[]
    {
        new DdjFlx4Mapping(),
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
