using Sholto.Interface.Controller.Mappings;

namespace Sholto.Host;

/// <summary>Builds the <see cref="ControllerStack"/>.</summary>
public interface IControllerStackFactory
{
    ControllerStack Build(ControllerMappingsOptions options);
}
