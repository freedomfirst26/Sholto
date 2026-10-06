namespace Sholto.Interface.MainUI.Controls.Knob;

/// <summary>Names the knob sweeps the Settings overlay uses, so no call site spells out tick lists.</summary>
public interface IKnobScaleFactory
{
    /// <summary>Backspin time, in seconds: ticks 0, 0.5, 1, 2, 3, steps of 0.05.</summary>
    IKnobScale BackspinTime();

    /// <summary>Backspin distance, in beats: ticks 0, 1, 2, 4, 8, 16, steps of 0.25.</summary>
    IKnobScale BackspinDistance();
}
