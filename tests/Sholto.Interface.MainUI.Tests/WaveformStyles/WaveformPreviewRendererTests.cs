using Sholto.Interface.MainUI.Controls.WaveformStyles;

namespace Sholto.Interface.MainUI.Tests.WaveformStyles;

public class WaveformPreviewRendererTests
{
    [Fact]
    public void Renders_the_whole_demo_track_for_each_style()
    {
        AvaloniaTestApp.EnsureStarted();
        var renderer = new WaveformPreviewRenderer();
        var demo = new DemoWaveformFactory();
        foreach (var style in new WaveformStylesFactory().Create().All)
        {
            using var bitmap = renderer.Render(style, demo.Peaks, new TestWaveformPalette().Create())!;
            Assert.Equal(demo.Peaks.Min.Length, bitmap.PixelSize.Width);
            Assert.Equal(256, bitmap.PixelSize.Height);
        }
    }
}
