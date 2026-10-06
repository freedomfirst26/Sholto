using Sholto.Data;

namespace Sholto.App.Performance;

/// <summary>The backspin feel: how long (<see cref="SetBackspinTime"/>) and how far in beats
/// (<see cref="SetBackspinDistance"/>) a flung platter coasts after the hand lets go. The scratch engine reads
/// <see cref="Seconds"/> and <see cref="Beats"/> when a fling launches; a change is announced as
/// <see cref="BackspinTimeChanged"/> / <see cref="BackspinDistanceChanged"/>. A user's choice (the command)
/// raises <see cref="TimeChosen"/> / <see cref="DistanceChosen"/> so the lifecycle can save it; a Restore from
/// settings does not, so a restored value is never written back.</summary>
public interface IBackspinFeel : ICommandHandler<SetBackspinTime>, ICommandHandler<SetBackspinDistance>
{
    /// <summary>The coast time in force, always within <see cref="SetBackspinTime.Min"/>..<see cref="SetBackspinTime.Max"/>.</summary>
    double Seconds { get; }

    /// <summary>The coast distance in force, always within <see cref="SetBackspinDistance.Min"/>..<see cref="SetBackspinDistance.Max"/>.</summary>
    double Beats { get; }

    /// <summary>Apply the time saved last time (clamped). Announced, but not raised as <see cref="TimeChosen"/>.</summary>
    void RestoreSeconds(double seconds);

    /// <summary>Apply the distance saved last time (clamped). Announced, but not raised as <see cref="DistanceChosen"/>.</summary>
    void RestoreBeats(double beats);

    /// <summary>A command changed the time; carries the clamped value now in force.</summary>
    event Action<double>? TimeChosen;

    /// <summary>A command changed the distance; carries the clamped value now in force.</summary>
    event Action<double>? DistanceChosen;
}
