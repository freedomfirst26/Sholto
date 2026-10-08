using Microsoft.Extensions.Options;
using Sholto.Interface.MainUI.Controls.WaveformStyles;

namespace Sholto.Interface.MainUI.Tests.WaveformStyles;

/// <summary>A baked style must look the same from frame to frame as the deck scrolls it; otherwise the
/// waveform flickers as it goes past (Sebastian's "mad flickering" on the first RGB comb, which drew a
/// 1-px stroke every second column). Measured as the deck draws: bilinear blit, at the display's device
/// scale (1 and the 1.25 of a 4K desktop with 120 dpi text scaling), from the snapped scroll position.</summary>
public class WaveformScrollShimmerTests
{
    private const double MaxSpread = 1.3;

    private readonly IWaveformStyles _styles = new WaveformStylesFactory(Options.Create(new WaveformStyleOptions())).Create();

    [Theory]
    [InlineData("rgb", 1.0)]
    [InlineData("rgb", 1.25)]
    [InlineData("three-band", 1.0)]
    [InlineData("three-band", 1.25)]
    public void Scrolling_barely_changes_the_picture_from_frame_to_frame(string styleId, double deviceScale)
    {
        using var image = _styles.ById(styleId).Bake(new TestWaveformPeaks().Create(), new TestWaveformPalette().Create(), CancellationToken.None)!;
        var (min, max) = new ScrollShimmer().Measure(image, deviceScale, snap: true);
        Assert.True(max / min < MaxSpread, $"{styleId} @{deviceScale}×: edge contrast swings {min:F0}..{max:F0} (×{max / min:F2}) across frames");
    }
}
