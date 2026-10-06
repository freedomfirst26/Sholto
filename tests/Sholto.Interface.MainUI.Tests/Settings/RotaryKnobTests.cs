using Avalonia;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using Avalonia.Platform;
using Sholto.Interface.MainUI.Controls;
using Sholto.Interface.MainUI.Controls.Knob;
using Sholto.Interface.MainUI.Theming;

namespace Sholto.Interface.MainUI.Tests;

/// <summary>The rotary knob draws its value ring in its palette's arc colour, from the stop round to the
/// value, over a track in the palette's track colour; and draws nothing without a palette.</summary>
public class RotaryKnobTests
{
    private readonly PiecewiseKnobScale _scale = new([0, 1, 2, 4, 8], 0.25);
    private readonly KnobPalette _palette;

    public RotaryKnobTests()
    {
        AvaloniaTestApp.EnsureStarted();
        // Distinct colours, so a pixel says which part of the knob it belongs to (the theme mapping is pinned
        // by KnobThemeTests).
        _palette = new KnobPalette(Arc: Colors.SpringGreen, Track: Colors.SlateGray, Cap: Colors.DarkSlateBlue,
            CapEdge: Colors.SlateGray, Tick: Colors.Silver, Pointer: Colors.White);
    }

    private Color Pixel(RotaryKnob knob, double degrees, double radius = 28)
    {
        knob.Measure(new Size(96, 96));
        knob.Arrange(new Rect(0, 0, 96, 96));
        var rtb = new RenderTargetBitmap(new PixelSize(96, 96));
        using (var ctx = rtb.CreateDrawingContext()) knob.Render(ctx);
        double rad = degrees * Math.PI / 180;
        int x = (int)Math.Round(48 + radius * Math.Sin(rad)), y = (int)Math.Round(48 - radius * Math.Cos(rad));
        var buffer = new byte[4];
        var native = System.Runtime.InteropServices.Marshal.AllocHGlobal(4);
        try
        {
            rtb.CopyPixels(new PixelRect(x, y, 1, 1), native, 4, 4);
            System.Runtime.InteropServices.Marshal.Copy(native, buffer, 0, 4);
        }
        finally { System.Runtime.InteropServices.Marshal.FreeHGlobal(native); }
        // BGRA, premultiplied: the ring pixel at its centre line is fully opaque.
        return Color.FromArgb(buffer[3], buffer[2], buffer[1], buffer[0]);
    }

    private RotaryKnob Knob(double value) => new() { Scale = _scale, Palette = _palette, Value = value };

    private Color Opaque(Color c) => Color.FromRgb(c.R, c.G, c.B);

    [Fact]
    public void At_eight_the_whole_ring_is_lit_in_the_theme_arc_colour()
    {
        var knob = Knob(8);
        Assert.Equal(Opaque(_palette.Arc), Pixel(knob, 90));
        Assert.Equal(Opaque(_palette.Arc), Pixel(knob, -90));
    }

    [Fact]
    public void At_zero_the_ring_is_all_track()
    {
        var knob = Knob(0);
        Assert.Equal(Opaque(_palette.Track), Pixel(knob, 90));
        Assert.Equal(Opaque(_palette.Track), Pixel(knob, -90));
    }

    [Fact]
    public void At_one_the_arc_runs_a_quarter_of_the_sweep()
    {
        // 1× sits at −67.5° (a quarter of 270° from −135°): lit before it, track after.
        var knob = Knob(1);
        Assert.Equal(Opaque(_palette.Arc), Pixel(knob, -100));
        Assert.Equal(Opaque(_palette.Track), Pixel(knob, -30));
    }

    [Fact]
    public void A_new_palette_redraws_in_its_colours()
    {
        var knob = Knob(8);
        var green = _palette with { Arc = Colors.Lime };
        knob.Palette = green;
        Assert.Equal(Colors.Lime, Pixel(knob, 90));
    }

    [Fact]
    public void Without_a_palette_nothing_is_drawn()
    {
        var knob = new RotaryKnob { Scale = _scale, Value = 8 };
        Assert.Equal(0, Pixel(knob, 90).A);
    }
}
