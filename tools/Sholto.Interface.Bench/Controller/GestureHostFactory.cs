using Microsoft.Extensions.Options;
using Sholto.Data;
using Sholto.Interface.Controller.Mappings;
using Sholto.Interface.Controller.Mappings.DdjFlx4;

namespace Sholto.Interface.Bench.Controller;

/// <summary>Builds each <see cref="GestureHost"/> over a fresh <see cref="ScriptedControlSurface"/>,
/// a fresh <see cref="ManualFrameClock"/> and the DDJ-FLX4 mapping.</summary>
/// <param name="controllers">The controller mapping options the FLX4 mapping is built with.</param>
public sealed class GestureHostFactory(IOptions<ControllerMappingsOptions> controllers) : IGestureHostFactory
{
    private readonly IOptions<ControllerMappingsOptions> _controllers = controllers;

    public GestureHost Create() =>
        new(new ScriptedControlSurface(), new ManualFrameClock(), new DdjFlx4Mapping(_controllers.Value.DdjFlx4));
}
