
using Microsoft.Extensions.Options;

namespace Sholto.Interface.MainUI.Controls.WaveformStyles;

/// <summary>The recipe for the shipped styles: 3-BAND (default) then RGB. Both calibrate over one band
/// scaler; 3-BAND draws the smoothed envelope, RGB the raw per-column comb.</summary>
public sealed class WaveformStylesFactory(IOptions<WaveformStyleOptions> options) : IWaveformStylesFactory
{
    private readonly WaveformStyleOptions _options = options.Value;

    public IWaveformStyles Create()
    {
        IWaveformBandScaler scaler = new WaveformBandScaler();
        return new WaveformStyles([
            new ThreeBandWaveformStrategy(new WaveformEnvelopeBuilder(scaler), new WaveformEnvelopeRenderer()),
            new RgbWaveformStrategy(scaler, new RgbColourMixer(_options.RgbFloorShare), _options.RgbColourRadius),
        ]);
    }
}
