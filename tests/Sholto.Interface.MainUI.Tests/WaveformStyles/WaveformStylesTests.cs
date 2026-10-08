using Microsoft.Extensions.Options;
using Sholto.Interface.MainUI.Controls.WaveformStyles;

namespace Sholto.Interface.MainUI.Tests.WaveformStyles;

public class WaveformStylesTests
{
    private readonly IWaveformStyles _styles = new WaveformStylesFactory(Options.Create(new WaveformStyleOptions())).Create();

    [Fact]
    public void Three_band_then_rgb_with_three_band_the_default()
    {
        Assert.Equal(["three-band", "rgb"], _styles.All.Select(s => s.Id));
        Assert.Equal(["3-BAND", "RGB"], _styles.All.Select(s => s.DisplayName));
        Assert.Same(_styles.All[0], _styles.Default);
        Assert.IsType<ThreeBandWaveformStrategy>(_styles.Default);
    }

    [Fact]
    public void ById_finds_each_style()
    {
        Assert.IsType<ThreeBandWaveformStrategy>(_styles.ById("three-band"));
        Assert.IsType<RgbWaveformStrategy>(_styles.ById("rgb"));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("blue")]
    [InlineData("RGB")]
    public void An_unknown_or_missing_id_falls_back_to_the_default(string? id)
    {
        Assert.Same(_styles.Default, _styles.ById(id));
    }
}
