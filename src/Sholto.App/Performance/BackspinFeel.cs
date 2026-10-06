using Sholto.Data;

namespace Sholto.App.Performance;

/// <inheritdoc />
/// <remarks>Starts at <see cref="SetBackspinTime.Default"/> and <see cref="SetBackspinDistance.Default"/> and
/// announces both at once, so a subscriber that arrives before any restore still learns the values. A value equal
/// to the one in force changes nothing and announces nothing; NaN is treated as the default.</remarks>
public sealed class BackspinFeel : IBackspinFeel
{
    private readonly IEventPublisher _publisher;

    public BackspinFeel(IEventPublisher publisher)
    {
        _publisher = publisher;
        Seconds = SetBackspinTime.Default;
        Beats = SetBackspinDistance.Default;
        _publisher.Publish(new BackspinTimeChanged(Seconds));
        _publisher.Publish(new BackspinDistanceChanged(Beats));
    }

    public double Seconds { get; private set; }

    public double Beats { get; private set; }

    public event Action<double>? TimeChosen;

    public event Action<double>? DistanceChosen;

    public void Handle(in SetBackspinTime command)
    {
        if (ApplySeconds(command.Seconds)) TimeChosen?.Invoke(Seconds);
    }

    public void Handle(in SetBackspinDistance command)
    {
        if (ApplyBeats(command.Beats)) DistanceChosen?.Invoke(Beats);
    }

    public void RestoreSeconds(double seconds) => ApplySeconds(seconds);

    public void RestoreBeats(double beats) => ApplyBeats(beats);

    private bool ApplySeconds(double seconds)
    {
        var clamped = double.IsNaN(seconds)
            ? SetBackspinTime.Default
            : Math.Clamp(seconds, SetBackspinTime.Min, SetBackspinTime.Max);
        if (clamped == Seconds) return false;
        Seconds = clamped;
        _publisher.Publish(new BackspinTimeChanged(Seconds));
        return true;
    }

    private bool ApplyBeats(double beats)
    {
        var clamped = double.IsNaN(beats)
            ? SetBackspinDistance.Default
            : Math.Clamp(beats, SetBackspinDistance.Min, SetBackspinDistance.Max);
        if (clamped == Beats) return false;
        Beats = clamped;
        _publisher.Publish(new BackspinDistanceChanged(Beats));
        return true;
    }
}
