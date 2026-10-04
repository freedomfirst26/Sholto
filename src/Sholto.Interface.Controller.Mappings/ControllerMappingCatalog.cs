using Sholto.Interface.Controller.Mappings.DdjFlx4;

namespace Sholto.Interface.Controller.Mappings;

/// <summary>
/// The one place supported devices are listed. To add a controller, create a
/// folder beside <c>DdjFlx4</c>, add its options property to
/// <see cref="ControllerMappingsOptions"/>, and add one entry to
/// <see cref="Mappings"/>. The composition root hands the result to
/// <see cref="MappingRegistry"/>.
/// </summary>
public sealed class ControllerMappingCatalog(ControllerMappingsOptions options)
{
    private readonly ControllerMappingsOptions _options = options;

    /// <summary>One mapping per supported device, built from the configured options.</summary>
    public IReadOnlyList<IControllerMapping> Mappings => [new DdjFlx4Mapping(_options.DdjFlx4)];
}
