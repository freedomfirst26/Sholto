
namespace Sholto.Interface.MainUI.Controls.WaveformStyles;

/// <summary>The recipe for the shipped styles: 3-BAND (default) then RGB. Both calibrate over one band
/// scaler; 3-BAND draws the smoothed envelope, RGB the raw per-column comb.</summary>
public sealed class WaveformStylesFactory : IWaveformStylesFactory
{
    public IWaveformStyles Create()
    {
        IWaveformBandScaler scaler = new WaveformBandScaler();
        return new WaveformStyles([
            new ThreeBandWaveformStrategy(new WaveformEnvelopeBuilder(scaler), new WaveformEnvelopePainter()),
            new RgbWaveformStrategy(scaler, new RgbColourMixer()),
        ]);
    }
}
