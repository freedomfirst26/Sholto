using Sholto.Interface.Bench.Scenario;

namespace Sholto.Interface.Bench.Headless;

/// <summary>Builds the headless graph and runs scenarios against it. Every returned
/// <see cref="HeadlessSession"/> is owned by the caller, who disposes it once done reading it
/// (disposing unloads its decks and closes their source files).</summary>
public interface IHeadlessHost
{
    /// <summary>Builds the graph and runs <paramref name="scenario"/> against it.</summary>
    HeadlessSession RunScenario(Scenario.Scenario scenario);

    /// <summary>Builds the graph and runs <paramref name="scenario"/> against it, rendering the mix to
    /// <paramref name="outWavPath"/> as the scenario's "wait" steps advance time.</summary>
    HeadlessSession RunScenario(Scenario.Scenario scenario, string outWavPath);

    /// <summary>Builds the graph with no scenario run, ready to drive.</summary>
    HeadlessSession Open();
}
