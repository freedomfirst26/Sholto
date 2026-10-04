using Avalonia.Threading;
using Sholto.Data;

namespace Sholto.Interface.MainUI;

/// <summary>The app's frame clock over Avalonia's dispatcher: one 16 ms <see cref="DispatcherTimer"/>
/// (the interval and priority the Orchestrator's own timer used), started by the first subscriber.
/// Handlers run in order on the UI thread.</summary>
public sealed class DispatcherFrameClock : IFrameClock
{
    private readonly FrameTickDispatcher _handlers = new();
    private DispatcherTimer? _timer;

    public DateTime Now => DateTime.UtcNow;

    public void Subscribe(IFrameTickHandler handler, int order)
    {
        _handlers.Add(handler, order);
        if (_timer is not null) return;
        _timer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(16) };
        _timer.Tick += (_, _) => _handlers.Dispatch(Now);
        _timer.Start();
    }
}
