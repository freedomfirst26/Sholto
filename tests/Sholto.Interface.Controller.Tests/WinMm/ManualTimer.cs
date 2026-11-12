namespace Sholto.Interface.Controller.Tests.WinMm;

/// <summary>The <see cref="ITimer"/> a <see cref="ManualTimeProvider"/> makes: runs its callback when the provider
/// advances past its due time, and never after <see cref="Dispose"/>.</summary>
public sealed class ManualTimer(TimerCallback callback, object? state, TimeSpan due, TimeSpan period) : ITimer
{
    private readonly TimerCallback _callback = callback;
    private readonly object? _state = state;
    private TimeSpan _due = due;
    private TimeSpan _period = period;

    public bool IsDisposed { get; private set; }
    public TimeSpan Period => _period;
    public int Fired { get; private set; }

    public void FireUntil(TimeSpan end)
    {
        while (!IsDisposed && _due <= end)
        {
            Fired++;
            _callback(_state);
            if (_period <= TimeSpan.Zero) { Dispose(); break; }
            _due += _period;
        }
    }

    public bool Change(TimeSpan dueTime, TimeSpan period)
    {
        _due = dueTime;
        _period = period;
        return !IsDisposed;
    }

    public void Dispose() => IsDisposed = true;

    public ValueTask DisposeAsync()
    {
        Dispose();
        return ValueTask.CompletedTask;
    }
}
