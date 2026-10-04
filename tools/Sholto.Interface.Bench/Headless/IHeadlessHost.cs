using Sholto.Interface.Bench.Scenario;

namespace Sholto.Interface.Bench.Headless;

/// <summary>Builds the headless graph and runs scenarios against it.</summary>
public interface IHeadlessHost
{
    /// <summary>Builds the graph and runs <paramref name="scenario"/> against it.</summary>
    HeadlessSession RunScenario(Scenario.Scenario scenario);

    /// <summary>Builds the graph with no scenario run, ready to drive.</summary>
    HeadlessSession Open();
}
