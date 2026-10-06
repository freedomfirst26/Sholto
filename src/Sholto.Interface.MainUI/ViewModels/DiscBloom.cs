using Sholto.Data;

namespace Sholto.Interface.MainUI.ViewModels;

/// <summary>Smooths three band energies into three glow opacities. Each band chases its target with a fast
/// attack (about 30 ms) and a slower release (about 250 ms); the outputs are scaled like the design mockup
/// (low x1.15 clamped to 1, mid x0.8, high x0.9). <see cref="Changed"/> fires only when an opacity moved by
/// one 8-bit step, so a held value costs nothing. Plain fields and arithmetic: no allocation per frame.</summary>
public sealed class DiscBloom : IDiscBloom
{
    private const double AttackSeconds = 0.030;
    private const double ReleaseSeconds = 0.250;
    private const double LowScale = 1.15;
    private const double MidScale = 0.8;
    private const double HighScale = 0.9;
    // A stalled frame (window drag, GC) must not jump the glows: cap the step.
    private const double MaxStepSeconds = 0.1;

    private readonly IFrameClock _clock;
    private DateTime _last;
    private double _low, _mid, _high;       // smoothed energies, 0..1
    private int _lowStep = -1, _midStep = -1, _highStep = -1;

    public DiscBloom(IFrameClock clock)
    {
        _clock = clock;
        _last = clock.Now;
    }

    public double Low => Math.Min(1.0, _low * LowScale);
    public double Mid => _mid * MidScale;
    public double High => _high * HighScale;

    public event Action? Changed;

    public void Advance(float low, float mid, float high)
    {
        var now = _clock.Now;
        var dt = Math.Clamp((now - _last).TotalSeconds, 0.0, MaxStepSeconds);
        _last = now;

        _low = Chase(_low, low, dt);
        _mid = Chase(_mid, mid, dt);
        _high = Chase(_high, high, dt);

        var lowStep = Step(Low);
        var midStep = Step(Mid);
        var highStep = Step(High);
        if (lowStep == _lowStep && midStep == _midStep && highStep == _highStep) return;
        _lowStep = lowStep;
        _midStep = midStep;
        _highStep = highStep;
        Changed?.Invoke();
    }

    private double Chase(double current, double target, double dt)
    {
        var tau = target > current ? AttackSeconds : ReleaseSeconds;
        return current + (target - current) * (1.0 - Math.Exp(-dt / tau));
    }

    private int Step(double opacity) => (int)(Math.Clamp(opacity, 0.0, 1.0) * 255.0 + 0.5);
}
