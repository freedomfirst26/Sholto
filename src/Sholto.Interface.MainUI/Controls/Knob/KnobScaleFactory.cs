namespace Sholto.Interface.MainUI.Controls.Knob;

/// <inheritdoc />
public sealed class KnobScaleFactory : IKnobScaleFactory
{
    public IKnobScale BackspinTime() => new PiecewiseKnobScale([0, 0.5, 1, 2, 3], 0.05);

    public IKnobScale BackspinDistance() => new PiecewiseKnobScale([0, 1, 2, 4, 8, 16], 0.25);
}
