using Sholto.App.Performance;
using Sholto.Data;
using Xunit;

namespace Sholto.App.Tests;

public class BackspinFeelTests
{
    private static readonly Origin Origin = new(InterfaceIds.Bench, "test", "backspin");

    private readonly DataBus _bus = new(new ThrowingFailureSink());
    private readonly RecordingHandler<BackspinTimeChanged> _times = new();
    private readonly RecordingHandler<BackspinDistanceChanged> _distances = new();
    private readonly BackspinFeel _feel;

    public BackspinFeelTests()
    {
        _feel = new BackspinFeel(_bus);
        _bus.Subscribe(_times);
        _bus.Subscribe(_distances);
        _times.Received.Clear();       // the replay of the defaults on subscribing
        _distances.Received.Clear();
    }

    [Fact]
    public void Starts_at_the_defaults()
    {
        Assert.Equal(0.6, _feel.Seconds);
        Assert.Equal(2.0, _feel.Beats);
    }

    [Theory]
    [InlineData(99.0, SetBackspinTime.Max)]
    [InlineData(-1.0, SetBackspinTime.Min)]
    [InlineData(1.5, 1.5)]
    public void Time_is_clamped(double sent, double expected)
    {
        _feel.Handle(new SetBackspinTime(sent, Origin));
        Assert.Equal(expected, _feel.Seconds);
    }

    [Theory]
    [InlineData(99.0, SetBackspinDistance.Max)]
    [InlineData(-1.0, SetBackspinDistance.Min)]
    [InlineData(4.25, 4.25)]
    public void Distance_is_clamped(double sent, double expected)
    {
        _feel.Handle(new SetBackspinDistance(sent, Origin));
        Assert.Equal(expected, _feel.Beats);
    }

    [Fact]
    public void Nan_becomes_the_default()
    {
        _feel.Handle(new SetBackspinTime(2.0, Origin));
        _feel.Handle(new SetBackspinDistance(8.0, Origin));
        _feel.Handle(new SetBackspinTime(double.NaN, Origin));
        _feel.Handle(new SetBackspinDistance(double.NaN, Origin));
        Assert.Equal(SetBackspinTime.Default, _feel.Seconds);
        Assert.Equal(SetBackspinDistance.Default, _feel.Beats);
    }

    [Fact]
    public void A_choice_announces_and_raises_chosen_with_the_clamped_value()
    {
        var chosenTimes = new List<double>();
        var chosenDistances = new List<double>();
        _feel.TimeChosen += chosenTimes.Add;
        _feel.DistanceChosen += chosenDistances.Add;

        _feel.Handle(new SetBackspinTime(50, Origin));
        _feel.Handle(new SetBackspinDistance(50, Origin));

        Assert.Equal([3.0], chosenTimes);
        Assert.Equal([16.0], chosenDistances);
        Assert.Equal(3.0, Assert.Single(_times.Received).Seconds);
        Assert.Equal(16.0, Assert.Single(_distances.Received).Beats);
    }

    [Fact]
    public void A_repeat_announces_nothing()
    {
        var raised = 0;
        _feel.TimeChosen += _ => raised++;
        _feel.DistanceChosen += _ => raised++;

        _feel.Handle(new SetBackspinTime(0.6, Origin));        // the default, already in force
        _feel.Handle(new SetBackspinDistance(2.0, Origin));
        _feel.Handle(new SetBackspinTime(1.0, Origin));
        _feel.Handle(new SetBackspinTime(1.0, Origin));
        _feel.Handle(new SetBackspinDistance(4.0, Origin));
        _feel.Handle(new SetBackspinDistance(4.0, Origin));

        Assert.Equal(2, raised);
        Assert.Single(_times.Received);
        Assert.Single(_distances.Received);
    }

    [Fact]
    public void Restore_applies_and_announces_without_raising_chosen()
    {
        var raised = 0;
        _feel.TimeChosen += _ => raised++;
        _feel.DistanceChosen += _ => raised++;

        _feel.RestoreSeconds(2.5);
        _feel.RestoreBeats(99);

        Assert.Equal(0, raised);
        Assert.Equal(2.5, _feel.Seconds);
        Assert.Equal(16.0, _feel.Beats);
        Assert.Equal(2.5, Assert.Single(_times.Received).Seconds);
        Assert.Equal(16.0, Assert.Single(_distances.Received).Beats);
    }

    [Fact]
    public void A_late_subscriber_gets_the_replayed_state()
    {
        _feel.Handle(new SetBackspinTime(1.25, Origin));
        _feel.Handle(new SetBackspinDistance(6.0, Origin));

        var lateTimes = new RecordingHandler<BackspinTimeChanged>();
        var lateDistances = new RecordingHandler<BackspinDistanceChanged>();
        _bus.Subscribe(lateTimes);
        _bus.Subscribe(lateDistances);

        Assert.Equal(1.25, Assert.Single(lateTimes.Received).Seconds);
        Assert.Equal(6.0, Assert.Single(lateDistances.Received).Beats);
    }
}
