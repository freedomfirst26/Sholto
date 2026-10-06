using Sholto.Interface.Controller;
using Sholto.App.Performance;

namespace Sholto.Host;

/// <summary>The input pipeline: the performance buckets the platter commands land on, and the
/// controller's LED adapter.</summary>
public sealed class InputStack(PerformanceStack performance, IControllerFeedback feedback) : IDisposable
{
    public PerformanceStack Performance { get; } = performance;
    /// <summary>The controller's LED adapter, already subscribed to the bus.</summary>
    public IControllerFeedback Feedback { get; } = feedback;

    public void Dispose()
    {
        Performance.Tick.Dispose();
        Performance.Scratch.Dispose();
    }
}
