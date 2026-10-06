using Sholto.Interface.MainUI.Controls.Knob;

namespace Sholto.Interface.MainUI.Tests;

/// <summary>The backspin knob's sweep: ticks 0, 1, 2, 4, 8 a quarter of the sweep apart, linear in between,
/// snapping to 0.25.</summary>
public class PiecewiseKnobScaleTests
{
    private readonly PiecewiseKnobScale _scale = new([0, 1, 2, 4, 8], 0.25);

    [Theory]
    [InlineData(0, 0)]
    [InlineData(0.5, 0.125)]
    [InlineData(1, 0.25)]
    [InlineData(2, 0.5)]
    [InlineData(3, 0.625)]
    [InlineData(4, 0.75)]
    [InlineData(6, 0.875)]
    [InlineData(8, 1)]
    [InlineData(-2, 0)]
    [InlineData(20, 1)]
    public void Each_tick_interval_gets_an_equal_share_of_the_sweep(double value, double fraction)
    {
        Assert.Equal(fraction, _scale.ToFraction(value), 9);
    }

    [Theory]
    [InlineData(0.25)]
    [InlineData(1.75)]
    [InlineData(3.5)]
    [InlineData(7.25)]
    public void From_fraction_inverts_to_fraction(double value)
    {
        Assert.Equal(value, _scale.FromFraction(_scale.ToFraction(value)), 9);
    }

    [Theory]
    [InlineData(1.1, 1.0)]
    [InlineData(1.13, 1.25)]
    [InlineData(1.125, 1.25)]
    [InlineData(7.9, 8.0)]
    [InlineData(-1, 0)]
    [InlineData(9, 8)]
    [InlineData(double.NaN, 0)]
    public void Snap_rounds_to_quarter_steps_inside_the_range(double value, double snapped)
    {
        Assert.Equal(snapped, _scale.Snap(value));
    }

    [Fact]
    public void Nudge_moves_whole_steps_and_stops_at_the_ends()
    {
        Assert.Equal(1.25, _scale.Nudge(1, +1));
        Assert.Equal(0.75, _scale.Nudge(1, -1));
        Assert.Equal(1.5, _scale.Nudge(1.1, +2));   // snaps first, then steps
        Assert.Equal(8, _scale.Nudge(8, +1));
        Assert.Equal(0, _scale.Nudge(0, -1));
    }

    [Fact]
    public void A_drag_lands_on_a_snapped_value()
    {
        Assert.Equal(1, _scale.SnapAt(0.25));
        Assert.Equal(0, _scale.SnapAt(-0.4));
        Assert.Equal(8, _scale.SnapAt(1.4));
        // A quarter of the way from 1 to 2 (fraction 0.3125) is 1.25.
        Assert.Equal(1.25, _scale.SnapAt(0.3125));
    }

    [Fact]
    public void Ticks_must_ascend()
    {
        Assert.Throws<ArgumentException>(() => new PiecewiseKnobScale([0, 2, 1], 0.25));
        Assert.Throws<ArgumentException>(() => new PiecewiseKnobScale([0], 0.25));
    }
}
