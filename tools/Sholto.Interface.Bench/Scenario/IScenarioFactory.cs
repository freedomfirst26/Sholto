namespace Sholto.Interface.Bench.Scenario;

/// <summary>Creates a validated <see cref="Scenario"/> from scenario JSON.</summary>
public interface IScenarioFactory
{
    Scenario Create(string json);

    Scenario CreateFromFile(string path);
}
