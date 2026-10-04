using System.Diagnostics;
using Sholto.Data;
using Sholto.Interface.Bench.Controller;
using Sholto.Interface.Bench.Headless;
using Sholto.Interface.Controller;

namespace Sholto.Interface.Bench.Behaviour;

/// <summary>C57 benchmark. Path 1: 10,000 top-platter jog turns from the controller side through
/// recognition, translation, <c>Send</c>, the platter handler and its accumulation, with one frame
/// (the per-frame flush) per 100 events. Path 2: 10,000 app state events through the bus to the
/// controller's LED handling. Warm-up first; allocation is the current thread's. The event object is
/// reused (in production the device mapping allocates it, outside this path). Result of path 1 carries
/// path 2 in <see cref="InputLatencyResult.Next"/>.</summary>
public sealed class InputLatencyBenchmark(IHeadlessHost host) : IInputLatencyBenchmark
{
    private const int Events = 10_000;
    private const int WarmUp = 2_000;
    private const int EventsPerFrame = 100;

    private readonly IHeadlessHost _host = host;

    public InputLatencyResult Run()
    {
        var host = _host.Open().Gestures;
        var turn = new ControllerEvent.JogRotated(0, 1, JogSource.SideRing);

        void JogBurst(int count)
        {
            for (var i = 0; i < count; i++)
            {
                host.Emit(turn);
                if ((i + 1) % EventsPerFrame == 0) host.Pump();
            }
        }

        var jog = Measure("jog turn: controller -> Send -> handler -> accumulate (+ flush per 100)", JogBurst);

        var bus = new DataBus(new BenchHandlerFailureSink());
        var feedback = new ControllerFeedback(new ScriptedControlSurface(), bus);
        feedback.Subscribe();

        void LedBurst(int count)
        {
            for (var i = 0; i < count; i++)
                bus.Publish(new DeckPlayStateChanged(i & 1, PlayPhase.Ending, (i & 2) == 0));
        }

        var led = Measure("app event -> controller LED", LedBurst);
        return jog with { Next = led };
    }

    private InputLatencyResult Measure(string path, Action<int> burst)
    {
        burst(WarmUp);
        GC.Collect();
        var bytesBefore = GC.GetAllocatedBytesForCurrentThread();
        var started = Stopwatch.GetTimestamp();
        burst(Events);
        var elapsed = Stopwatch.GetElapsedTime(started);
        var bytes = GC.GetAllocatedBytesForCurrentThread() - bytesBefore;
        return new InputLatencyResult(path, Events, elapsed.TotalNanoseconds / Events, (double)bytes / Events);
    }
}
