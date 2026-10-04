using Sholto.Interface.Controller.Mappings.DdjFlx4;

namespace Sholto.Interface.Controller.Mappings;

/// <summary>Configuration for every supported controller — one property per
/// device. Add a property here when you add a device.</summary>
public sealed class ControllerMappingsOptions
{
    public DdjFlx4Options DdjFlx4 { get; set; } = new();
}
