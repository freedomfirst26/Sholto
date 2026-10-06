using Avalonia.Media.Imaging;
using Sholto.Interface.MainUI.Theming;
using Sholto.Interface.MainUI.ViewModels;
using SkiaSharp;
using Sholto.Data;

namespace Sholto.Interface.MainUI.Controls.WaveformStyles;

/// <summary>Default <see cref="IWaveformPreviewRenderer"/>: bakes the whole track exactly as a deck would
/// (so the calibration matches what the decks show) and hands it back as one bitmap, a column per peak.</summary>
public sealed class WaveformPreviewRenderer : IWaveformPreviewRenderer
{
    public Bitmap? Render(IWaveformStyleStrategy style, WaveformPeaks peaks, WaveformPalette palette)
    {
        using var image = style.Bake(peaks, palette, CancellationToken.None);
        if (image is null) return null;
        using var data = image.Encode(SKEncodedImageFormat.Png, 100);
        using var stream = new MemoryStream(data.ToArray());
        return new Bitmap(stream);
    }
}
