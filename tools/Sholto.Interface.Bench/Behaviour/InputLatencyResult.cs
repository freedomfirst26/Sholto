namespace Sholto.Interface.Bench.Behaviour;

/// <summary>One measured path: events run, nanoseconds and bytes allocated per event.</summary>
public sealed record InputLatencyResult(
    string Path, int Events, double NanosecondsPerEvent, double BytesPerEvent)
{
    /// <summary>The second measured path (set by the benchmark).</summary>
    public InputLatencyResult? Next { get; init; }
}
