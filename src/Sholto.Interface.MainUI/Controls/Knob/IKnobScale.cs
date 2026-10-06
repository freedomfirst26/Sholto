namespace Sholto.Interface.MainUI.Controls.Knob;

/// <summary>How a rotary knob's value maps onto its sweep, and the steps it snaps to. A fraction is the
/// position along the sweep: 0 = fully anticlockwise, 1 = fully clockwise.</summary>
public interface IKnobScale
{
    double Minimum { get; }

    double Maximum { get; }

    /// <summary>The snap step (e.g. 0.25); every value the knob settles on is a multiple of it.</summary>
    double Step { get; }

    /// <summary>The labelled tick values, in ascending order (the first is <see cref="Minimum"/>, the last
    /// <see cref="Maximum"/>).</summary>
    IReadOnlyList<double> Ticks { get; }

    /// <summary>Value → sweep fraction (clamped to 0..1).</summary>
    double ToFraction(double value);

    /// <summary>Sweep fraction → value (unsnapped; the fraction is clamped to 0..1).</summary>
    double FromFraction(double fraction);

    /// <summary>The snapped value at sweep <paramref name="fraction"/> (clamped to 0..1): where a drag lands.</summary>
    double SnapAt(double fraction);

    /// <summary><paramref name="value"/> snapped, then moved <paramref name="steps"/> steps (negative = down),
    /// clamped to the range: the wheel and the arrow keys.</summary>
    double Nudge(double value, int steps);

    /// <summary>The nearest multiple of <see cref="Step"/>, clamped to the range. NaN → <see cref="Minimum"/>.</summary>
    double Snap(double value);
}
