using Microsoft.Extensions.Options;
using Sholto.App.Decks;

namespace Sholto.App.Tests;

/// <summary>Each deck's BEAT JUMP scale starts at 1, halves and doubles on its own, and stops at the bounds.</summary>
public class BeatJumpSizeTests
{
    private readonly BeatJumpSize _size = new(Options.Create(new BeatJumpOptions()));

    [Fact]
    public void Both_decks_start_at_one()
    {
        Assert.Equal(1.0, _size.ScaleFor(0));
        Assert.Equal(1.0, _size.ScaleFor(1));
    }

    [Fact]
    public void Halve_and_double_change_only_the_named_deck()
    {
        _size.Halve(0);
        Assert.Equal(0.5, _size.ScaleFor(0));
        Assert.Equal(1.0, _size.ScaleFor(1));

        _size.Double(1);
        _size.Double(1);
        Assert.Equal(0.5, _size.ScaleFor(0));
        Assert.Equal(4.0, _size.ScaleFor(1));
    }

    [Fact]
    public void Halving_stops_at_an_eighth_and_further_halving_leaves_it_there()
    {
        for (int i = 0; i < 3; i++) _size.Halve(0);
        Assert.Equal(0.125, _size.ScaleFor(0));

        _size.Halve(0);
        Assert.Equal(0.125, _size.ScaleFor(0));

        _size.Double(0);
        Assert.Equal(0.25, _size.ScaleFor(0));
    }

    [Fact]
    public void Doubling_stops_at_eight_and_further_doubling_leaves_it_there()
    {
        for (int i = 0; i < 3; i++) _size.Double(1);
        Assert.Equal(8.0, _size.ScaleFor(1));

        _size.Double(1);
        Assert.Equal(8.0, _size.ScaleFor(1));

        _size.Halve(1);
        Assert.Equal(4.0, _size.ScaleFor(1));
    }

    [Fact]
    public void The_bounds_come_from_the_options()
    {
        var size = new BeatJumpSize(Options.Create(new BeatJumpOptions { MinScale = 0.5, MaxScale = 2 }));

        size.Halve(0);
        size.Halve(0);
        size.Double(1);
        size.Double(1);

        Assert.Equal(0.5, size.ScaleFor(0));
        Assert.Equal(2.0, size.ScaleFor(1));
    }

    [Fact]
    public void An_unknown_deck_is_ignored_and_reads_as_one()
    {
        _size.Halve(5);
        _size.Double(-1);

        Assert.Equal(1.0, _size.ScaleFor(5));
        Assert.Equal(1.0, _size.ScaleFor(0));
        Assert.Equal(1.0, _size.ScaleFor(1));
    }
}
