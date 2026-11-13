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

    /// <summary>Where a render-time clock starts. Arbitrary but fixed, so a render is the same every run.</summary>
    private readonly DateTime _renderStart = new(2000, 1, 1, 0, 0, 0, DateTimeKind.Utc);

    public GestureHost Create() => Build(new ManualFrameClock());

    public GestureHost CreateForRender() => Build(new ManualFrameClock(_renderStart));

    private GestureHost Build(ManualFrameClock clock) =>
        new(new ScriptedControlSurface(), clock, new DdjFlx4Mapping(_controllers.Value.DdjFlx4));
}
