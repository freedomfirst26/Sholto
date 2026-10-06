using System.Reflection;
using Avalonia.Media;
using Sholto.Interface.MainUI.Controls;
using Sholto.Interface.MainUI.Theming;

namespace Sholto.Interface.MainUI.Tests.WaveformStyles;

/// <summary>The preview card's played-part fade and playhead take the tried-on theme's colours.</summary>
[Collection(AvaloniaXamlCollection.Name)]
public class WaveformPreviewControlTests
{
    public WaveformPreviewControlTests() => AvaloniaTestApp.EnsureStarted();

    private static IBrush? Field(WaveformPreviewControl control, string name) =>
        (IBrush?)typeof(WaveformPreviewControl)
            .GetField(name, BindingFlags.Instance | BindingFlags.NonPublic)!
            .GetValue(control);

    [Fact]
    public void Null_palette_has_no_fade_and_no_playhead()
    {
        var control = new WaveformPreviewControl();
        Assert.Null(Field(control, "_playedFade"));
        Assert.Null(Field(control, "_playhead"));
    }

    [Fact]
    public void Setting_the_palette_sets_the_fade_and_playhead_colours()
    {
        var palette = new TestWaveformPalette().Create() with
        {
            Background = Color.FromRgb(0x10, 0x20, 0x30),
            Playhead = Color.FromRgb(0xAA, 0xBB, 0xCC),
        };
        var control = new WaveformPreviewControl { Palette = palette };

        var fade = Assert.IsAssignableFrom<IGradientBrush>(Field(control, "_playedFade"));
        Assert.Equal(Color.FromArgb(0xD0, 0x10, 0x20, 0x30), fade.GradientStops[0].Color);
        Assert.Equal(Color.FromArgb(0x1A, 0x10, 0x20, 0x30), fade.GradientStops[1].Color);
        var playhead = Assert.IsAssignableFrom<ISolidColorBrush>(Field(control, "_playhead"));
        Assert.Equal(Color.FromRgb(0xAA, 0xBB, 0xCC), playhead.Color);
    }

    [Fact]
    public void Changing_the_palette_changes_the_brush_colours()
    {
        var first = new TestWaveformPalette().Create() with { Playhead = Colors.Red };
        var control = new WaveformPreviewControl { Palette = first };
        control.Palette = first with { Playhead = Colors.Blue, Background = Colors.Green };

        Assert.Equal(Colors.Blue, Assert.IsAssignableFrom<ISolidColorBrush>(Field(control, "_playhead")).Color);
        var fade = Assert.IsAssignableFrom<IGradientBrush>(Field(control, "_playedFade"));
        Assert.Equal(Color.FromArgb(0xD0, 0x00, 0x80, 0x00), fade.GradientStops[0].Color);
    }
}
