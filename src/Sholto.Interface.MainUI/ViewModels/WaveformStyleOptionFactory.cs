using Avalonia.Media;
using Sholto.Interface.MainUI.Controls.WaveformStyles;
using Sholto.Interface.MainUI.Theming;

namespace Sholto.Interface.MainUI.ViewModels;

/// <summary>Default <see cref="IWaveformStyleOptionFactory"/>: the wizard copy per style id.</summary>
public sealed class WaveformStyleOptionFactory : IWaveformStyleOptionFactory
{
    public WaveformStyleOption Create(IWaveformStyleStrategy style, int index, bool isCurrent, WaveformPalette palette)
    {
        var key = (index + 1).ToString(System.Globalization.CultureInfo.InvariantCulture);
        return style.Id switch
        {
            "three-band" => new WaveformStyleOption(style, key, isCurrent ? "CURRENT" : null, isCurrent,
                "Low, mid and high drawn as layers, one inside the other. Each band's height is easy to read, " +
                "so you can see the kick and the bassline separately.",
                Legend(style, palette)),
            "rgb" => new WaveformStyleOption(style, key, isCurrent ? "CURRENT" : "NEW", isCurrent,
                "Fine stripes, each coloured by the mix of bass, mids and highs at that instant, in your theme's " +
                "colours. Breakdowns and drops stand out by colour, even across the room.",
                Legend(style, palette)),
            _ => new WaveformStyleOption(style, key, isCurrent ? "CURRENT" : null, isCurrent, "", []),
        };
    }

    public IReadOnlyList<WaveformLegendEntry> Legend(IWaveformStyleStrategy style, WaveformPalette palette) => style.Id switch
    {
        "three-band" => [Entry("Low", palette.Low), Entry("Mid", palette.Mid), Entry("High", palette.High)],
        "rgb" => [Entry("Bass", palette.RgbLow), Entry("Mids", palette.RgbMid), Entry("Highs", palette.RgbHigh)],
        _ => [],
    };

    private WaveformLegendEntry Entry(string label, Color colour) => new(label, new SolidColorBrush(colour));
}
