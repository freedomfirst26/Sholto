namespace Sholto.Data;

/// <summary>The backspin time, in seconds, now in force (see <see cref="SetBackspinTime"/>). State: a late
/// subscriber is told the current value, so a settings view opened at any time shows it.</summary>
public readonly record struct BackspinTimeChanged(double Seconds) : IStateEvent
{
    public int Slot => 0;
}
