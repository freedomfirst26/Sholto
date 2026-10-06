using Avalonia.Media.Imaging;
using Sholto.Interface.MainUI.Controls.WaveformStyles;
using Sholto.Interface.MainUI.Theming;
using Sholto.Data;

namespace Sholto.Interface.MainUI.ViewModels;

/// <summary>Draws a whole track in a given waveform style, for the wizard's preview cards to scroll.
/// Slow (a whole-track bake): call it off the UI thread.</summary>
public interface IWaveformPreviewRenderer
{
    /// <returns>Null when there is nothing to draw.</returns>
    Bitmap? Render(IWaveformStyleStrategy style, WaveformPeaks peaks, WaveformPalette palette);
}
