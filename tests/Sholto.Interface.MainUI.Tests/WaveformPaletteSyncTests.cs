using Avalonia.Media;
using Microsoft.Extensions.Options;
using Sholto.Interface.MainUI.Controls;
using Sholto.Interface.MainUI.Controls.WaveformStyles;
using Sholto.Interface.MainUI.Theming;
using Sholto.Interface.MainUI.Tests.WaveformStyles;
using SkiaSharp;

namespace Sholto.Interface.MainUI.Tests;

public class WaveformPaletteSyncTests
{
    private readonly IWaveformPaletteSync _sync = new WaveformPaletteSync();
    private readonly WaveformPalette _p = new TestWaveformPalette().Create();
    private readonly IWaveformStyleStrategy _style =
        new WaveformStylesFactory(Options.Create(new WaveformStyleOptions())).Create().ById("three-band");

    [Fact]
    public void NeedsRebake_WhenNothingBaked() => Assert.True(_sync.NeedsRebake(_p, _style));

    [Fact]
    public void NeedsRebake_FalseWhenStyleBakesSameColours()
    {
        Assert.NotEqual(Colors.Blue, _p.Playhead);
        _sync.MarkBaked(_p);
        Assert.False(_sync.NeedsRebake(_p with { Playhead = Colors.Blue }, _style));
    }

    [Fact]
    public void NeedsRebake_TrueWhenBakedColoursDiffer()
    {
        Assert.NotEqual(Colors.Blue, _p.Low);
        _sync.MarkBaked(_p);
        Assert.True(_sync.NeedsRebake(_p with { Low = Colors.Blue }, _style));
    }

    [Fact]
    public void NeedsRebake_TrueWithoutStyle()
    {
        _sync.MarkBaked(_p);
        Assert.True(_sync.NeedsRebake(_p, null));
    }

    [Fact]
    public void LoopColour_NudgedIsGridEditAtA0()
    {
        Assert.Equal(new SKColor(_p.GridEdit.R, _p.GridEdit.G, _p.GridEdit.B, 0xA0), _sync.LoopColour(_p, true));
        Assert.Equal(new SKColor(_p.Loop.R, _p.Loop.G, _p.Loop.B, _p.Loop.A), _sync.LoopColour(_p, false));
    }

    [Fact]
    public void LiveColours_AppliesTodaysAlphas()
    {
        var live = _sync.LiveColours(_p);
        Assert.Equal(0xD0, live.VocalActive.Alpha);
        Assert.Equal(0xB0, live.VocalInactive.Alpha);
        Assert.Equal(0xF0, live.Glow.Alpha);
        Assert.Equal(0x99, live.Edge.Alpha);
        Assert.Equal(_p.Playhead.A, live.Playhead.Alpha);
    }

    [Fact]
    public void GridEditPen_IsCachedPerPalette()
    {
        var pen = _sync.GridEditPen(_p);
        Assert.Same(pen, _sync.GridEditPen(_p));
        Assert.Equal(2, pen.Thickness);
        var brush = Assert.IsAssignableFrom<ISolidColorBrush>(pen.Brush);
        Assert.Equal(Color.FromArgb(0xE0, _p.GridEdit.R, _p.GridEdit.G, _p.GridEdit.B), brush.Color);
        Assert.NotSame(pen, _sync.GridEditPen(_p with { GridEdit = Colors.Blue }));
    }
}
