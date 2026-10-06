using Sholto.Interface.MainUI.Controls.WaveformStyles;
using Sholto.Interface.MainUI.Theming;

namespace Sholto.Interface.MainUI.ViewModels;

/// <summary>Builds a wizard card for a style: its copy, tag and legend (coloured from the palette).</summary>
public interface IWaveformStyleOptionFactory
{
    /// <param name="index">Position in the list, 0-based; the card's key is index + 1.</param>
    /// <param name="isCurrent">The style in use when the wizard opened.</param>
    WaveformStyleOption Create(IWaveformStyleStrategy style, int index, bool isCurrent, WaveformPalette palette);

    /// <summary>The legend swatches for a style, coloured from the palette.</summary>
    IReadOnlyList<WaveformLegendEntry> Legend(IWaveformStyleStrategy style, WaveformPalette palette);
}
