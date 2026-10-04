using Sholto.Interface.Controller.Mappings;

namespace Sholto.Interface.MainUI;

/// <summary>Builds the <see cref="ControllerStack"/>.</summary>
public interface IControllerStackFactory
{
    ControllerStack Build(ControllerMappingsOptions options);
}
