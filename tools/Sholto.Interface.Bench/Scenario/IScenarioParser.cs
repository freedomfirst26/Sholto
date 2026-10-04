namespace Sholto.Interface.Bench.Scenario;

/// <summary>Parses and validates a scenario file.</summary>
public interface IScenarioParser
{
    Scenario LoadFile(string path);

    Scenario Parse(string json);
}
