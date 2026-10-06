namespace Sholto.Data;

/// <summary>The backspin distance, in beats, now in force (see <see cref="SetBackspinDistance"/>). State: a late
/// subscriber is told the current value, so a settings view opened at any time shows it.</summary>
public readonly record struct BackspinDistanceChanged(double Beats) : IStateEvent
{
    public int Slot => 0;
}
