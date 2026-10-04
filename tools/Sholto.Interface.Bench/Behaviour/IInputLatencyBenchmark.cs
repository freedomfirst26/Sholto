namespace Sholto.Interface.Bench.Behaviour;

/// <summary>Times the input hot paths: cost and allocation per event.</summary>
public interface IInputLatencyBenchmark
{
    InputLatencyResult Run();
}
