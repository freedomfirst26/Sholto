namespace Sholto.Interface.Controller.Tests.WinMm;

/// <summary>A <see cref="TimeProvider"/> whose timers fire only when a test says so: <see cref="Advance"/> fires
/// every live timer whose next due time has passed, as often as the elapsed time covers its period.</summary>
public sealed class ManualTimeProvider : TimeProvider
{
    private readonly List<ManualTimer> _timers = [];

    public TimeSpan Now { get; private set; }

    /// <summary>Every timer made so far, disposed or not.</summary>
    public IReadOnlyList<ManualTimer> Timers => _timers;

    public override ITimer CreateTimer(TimerCallback callback, object? state, TimeSpan dueTime, TimeSpan period)
    {
        var timer = new ManualTimer(callback, state, Now + dueTime, period);
        _timers.Add(timer);
        return timer;
    }

    public void Advance(TimeSpan by)
    {
        var end = Now + by;
        foreach (var timer in _timers.ToList())
            timer.FireUntil(end);
        Now = end;
    }
}
