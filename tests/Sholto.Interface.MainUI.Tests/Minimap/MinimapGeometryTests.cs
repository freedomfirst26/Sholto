using Sholto.Interface.MainUI.Controls.Minimap;

namespace Sholto.Interface.MainUI.Tests.Minimap;

public class MinimapGeometryTests
{
    private readonly MinimapGeometry _g = new();

    [Fact]
    public void The_playhead_and_played_width_are_the_position_fraction_of_the_strip()
    {
        Assert.Equal(250, _g.X(0.25, 1000));
        Assert.Equal(0, _g.X(-1, 1000));
        Assert.Equal(1000, _g.X(7, 1000));
    }

    [Fact]
    public void A_region_maps_seconds_to_x_and_rejects_empty_or_unknown_lengths()
    {
        var r = _g.Region(30, 60, 300, 1000)!.Value;
        Assert.Equal(100, r.Left, 9);
        Assert.Equal(200, r.Right, 9);
        Assert.Null(_g.Region(60, 60, 300, 1000));
        Assert.Null(_g.Region(0, 10, 0, 1000));
        Assert.Equal(333.3333, _g.XOfSeconds(100, 300, 1000), 3);
    }

    [Fact]
    public void A_bar_maps_to_x_through_the_first_downbeat_and_the_bar_period()
    {
        // 2 s per bar from 4 s in, over a 100 s track on a 1000-wide strip.
        Assert.Equal(40, _g.XOfBar(0, 4, 2, 100, 1000), 9);
        Assert.Equal(60, _g.XOfBar(1, 4, 2, 100, 1000), 9);
        Assert.Equal(1000, _g.XOfBar(500, 4, 2, 100, 1000));   // past the end clamps
        Assert.Equal(0, _g.XOfBar(3, 4, 2, 0, 1000));          // unknown track length
    }
}
