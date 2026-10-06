using Sholto.Data;

namespace Sholto.Interface.MainUI.ViewModels;

/// <summary>Default <see cref="IWaveformPreviewScroll"/>, driven by the frame clock. The clock cannot
/// unsubscribe a handler, so this subscribes once, at construction, and does nothing between
/// <see cref="Stop"/> and the next <see cref="Start"/>. A frame allocates nothing.</summary>
public sealed class WaveformPreviewScroll : IWaveformPreviewScroll, IFrameTickHandler
{
    /// <summary>Order on the frame clock: after the performance tick (0), which must run first.</summary>
    private const int ClockOrder = 100;
    // A stalled UI thread must not make the preview jump.
    private const double MaxStepSeconds = 0.1;

    private double _position;
    private double _length;
    private double _columnsPerSecond;
    private bool _running;
    private bool _hasLast;
    private DateTime _last;

    public WaveformPreviewScroll(IFrameClock clock) => clock.Subscribe(this, ClockOrder);

    public double Position => _position;

    public double Length => _length;

    public bool IsRunning => _running;

    public event EventHandler? Moved;

    public void Start(double length, double columnsPerSecond, double startColumn)
    {
        _length = length;
        _columnsPerSecond = columnsPerSecond;
        _position = length > 0 ? startColumn % length : 0;
        _hasLast = false;
        _running = length > 0;
    }

    public void Stop() => _running = false;

    public void OnFrame(DateTime now)
    {
        if (!_running) return;
        if (!_hasLast)
        {
            _last = now;
            _hasLast = true;
            return;
        }
        double dt = Math.Clamp((now - _last).TotalSeconds, 0, MaxStepSeconds);
        _last = now;
        _position = (_position + dt * _columnsPerSecond) % _length;
        Moved?.Invoke(this, EventArgs.Empty);
    }
}
