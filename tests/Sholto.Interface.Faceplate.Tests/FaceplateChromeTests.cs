using Avalonia.Controls;
using Avalonia.Media;
using Sholto.Interface.Faceplate.Views;

namespace Sholto.Interface.Faceplate.Tests;

public class FaceplateChromeTests
{
    // Host is unattached, so every theme lookup falls back to Colors.Transparent (#00FFFFFF):
    // only alpha, offsets and geometry are asserted, never RGB.
    private readonly FaceplateChrome _chrome = new();
    private readonly Grid _backdrop = new();
    private readonly Border _feather = new();
    private readonly Border _panel = new();

    private void Paint() => _chrome.Paint(new Border(), _backdrop, _feather, _panel);

    [Fact]
    public void Paint_gives_the_backdrop_a_solid_scrim_at_F6_alpha()
    {
        Paint();

        var brush = Assert.IsType<SolidColorBrush>(_backdrop.Background);
        Assert.Equal(0xF6, brush.Color.A);
    }

    [Fact]
    public void Paint_gives_the_feather_three_stops_rising_to_C8()
    {
        Paint();

        var brush = Assert.IsType<LinearGradientBrush>(_feather.Background);
        Assert.Equal([0.0, 0.55, 1.0], brush.GradientStops.Select(s => s.Offset));
        Assert.Equal([(byte)0x00, (byte)0x70, (byte)0xC8], brush.GradientStops.Select(s => s.Color.A));
    }

    [Fact]
    public void Paint_gives_the_panel_a_three_stop_face_and_a_B3_shadow()
    {
        Paint();

        var face = Assert.IsType<LinearGradientBrush>(_panel.Background);
        Assert.Equal([0.0, 0.10, 1.0], face.GradientStops.Select(s => s.Offset));
        var shadow = _panel.BoxShadow[0];
        Assert.Equal(-24, shadow.OffsetX);
        Assert.Equal(64, shadow.Blur);
        Assert.Equal(0xB3, shadow.Color.A);
    }
}
