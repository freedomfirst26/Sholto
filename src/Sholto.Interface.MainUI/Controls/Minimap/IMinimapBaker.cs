using Sholto.Interface.MainUI.Controls.WaveformStyles;
using Sholto.Interface.MainUI.Theming;
using SkiaSharp;
using Sholto.Data;

namespace Sholto.Interface.MainUI.Controls.Minimap;

/// <summary>Bakes the minimap strip's still picture: the label row with a coloured underline per section,
/// a faint section tint, the whole track in the deck's waveform style squeezed to the strip width, phrase
/// lines over it, and dividers at the section boundaries. Everything that moves is drawn live over it.</summary>
public interface IMinimapBaker
{
    /// <summary>Null when there is nothing to draw (no peaks, or no room). The size is in device pixels;
    /// <paramref name="scale"/> is the device pixels per DIP, which sizes the label row and text.</summary>
    SKImage? Bake(
        WaveformPeaks peaks,
        MinimapStructure? structure,
        MinimapPalette palette,
        WaveformPalette waveform,
        IWaveformStyleStrategy style,
        int pixelWidth,
        int pixelHeight,
        double scale);
}
