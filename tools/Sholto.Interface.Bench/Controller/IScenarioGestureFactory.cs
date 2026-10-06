using Sholto.Interface.Bench.Scenario;
using Sholto.Interface.Controller;

namespace Sholto.Interface.Bench.Controller;

/// <summary>Turns one "gesture" scenario action into the <see cref="ControllerEvent"/> it names.</summary>
public interface IScenarioGestureFactory
{
    ControllerEvent Create(ScenarioAction a);
}
