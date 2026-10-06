namespace Sholto.Interface.MainUI.Controls.Knob;

/// <summary>A knob scale whose ticks sit an equal angle apart, linear in between. For ticks 0, 1, 2, 4, 8
/// each interval gets a quarter of the sweep, so the default 1× sits a quarter of the way round and the
/// fine low end gets as much travel as the coarse top (a linear 0..8 sweep would crowd 0–2× into the first
/// quarter).</summary>
public sealed class PiecewiseKnobScale : IKnobScale
{
    private readonly double[] _ticks;

    public PiecewiseKnobScale(IReadOnlyList<double> ticks, double step)
    {
        if (ticks.Count < 2) throw new ArgumentException("A knob scale needs at least two ticks.", nameof(ticks));
        for (int i = 1; i < ticks.Count; i++)
            if (!(ticks[i] > ticks[i - 1]))
                throw new ArgumentException("Knob ticks must be strictly ascending.", nameof(ticks));
        if (!(step > 0)) throw new ArgumentOutOfRangeException(nameof(step), "The snap step must be positive.");
        _ticks = ticks.ToArray();
        Step = step;
    }

    public double Minimum => _ticks[0];

    public double Maximum => _ticks[^1];

    public double Step { get; }

    public IReadOnlyList<double> Ticks => _ticks;

    public double ToFraction(double value)
    {
        if (double.IsNaN(value) || value <= Minimum) return 0;
        if (value >= Maximum) return 1;
        int intervals = _ticks.Length - 1;
        for (int i = 0; i < intervals; i++)
        {
            if (value <= _ticks[i + 1])
                return (i + (value - _ticks[i]) / (_ticks[i + 1] - _ticks[i])) / intervals;
        }
        return 1;
    }

    public double FromFraction(double fraction)
    {
        if (double.IsNaN(fraction) || fraction <= 0) return Minimum;
        if (fraction >= 1) return Maximum;
        int intervals = _ticks.Length - 1;
        double scaled = fraction * intervals;
        int i = Math.Min((int)scaled, intervals - 1);
        return _ticks[i] + (scaled - i) * (_ticks[i + 1] - _ticks[i]);
    }

    public double SnapAt(double fraction) => Snap(FromFraction(fraction));

    public double Nudge(double value, int steps) => Snap(Snap(value) + steps * Step);

    public double Snap(double value)
    {
        if (double.IsNaN(value)) return Minimum;
        var snapped = Math.Round(value / Step, MidpointRounding.AwayFromZero) * Step;
        return Math.Clamp(snapped, Minimum, Maximum);
    }
}
