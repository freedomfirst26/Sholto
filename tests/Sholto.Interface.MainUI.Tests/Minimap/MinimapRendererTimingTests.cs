using Microsoft.Extensions.Options;
using Sholto.Data;
using Sholto.Interface.MainUI.Controls.Minimap;
using Sholto.Interface.MainUI.Controls.WaveformStyles;
using Sholto.Interface.MainUI.Theming;
using Sholto.Interface.MainUI.Tests.WaveformStyles;
using SkiaSharp;

namespace Sholto.Interface.MainUI.Tests.Minimap;

/// <summary>The minimap places sections by turning peak columns into seconds, so the peaks' own sample
/// rate must set that scale: a 44.1 kHz track is not a 48 kHz one.</summary>
public class MinimapRendererTimingTests
{
    private const int Width = 640;
    private const int Height = 64;

    private readonly MinimapPalette _palette;

    public MinimapRendererTimingTests()
    {
        AvaloniaTestApp.EnsureStarted();
        _palette = new ThemeStackFactory().Build().Context.Current.Minimap;
    }

    [Fact]
    public void A_44100_Hz_track_places_the_section_boundary_by_44100_not_48000()
    {
        // 3200 peaks x 441 samples at 44100 Hz = exactly 32 s: 16 bars of 2 s, so bar 8 is the midpoint.
        var shape = new TestWaveformPeaks().Create(3200);
        var peaks = new WaveformPeaks(shape.Min, shape.Max, shape.Low, shape.Mid, shape.High, 441, 44100);
        var structure = new MinimapStructure(
            [new DeckSection(DeckSectionKind.Intro, 0, 8), new DeckSection(DeckSectionKind.Drop, 8, 8)],
            new DeckPhraseGrid(0, 8), 0, 2.0);
        var renderer = new MinimapRenderer(new MinimapPeakDownsampler(), new MinimapGeometry(), new MinimapPhraseLines(), new MinimapSectionLabels());

        using var image = renderer.Render(peaks, structure, _palette, new TestWaveformPalette().Create(),
            new WaveformStylesFactory(Options.Create(new WaveformStyleOptions())).Create().Default, Width, Height, 1);
        using var bitmap = SKBitmap.FromImage(image!);

        int underlineRow = (int)Math.Round(MinimapMetrics.LabelRowHeight) - 1;
        var intro = bitmap.GetPixel(2, underlineRow);
        int boundary = Enumerable.Range(0, Width).First(x => bitmap.GetPixel(x, underlineRow) != intro);

        Assert.InRange(boundary, Width / 2 - 1, Width / 2 + 1);
    }
}
