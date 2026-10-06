using SkiaSharp;

namespace Sholto.Interface.MainUI.Controls.WaveformStyles;

/// <summary>Fills one binned envelope as a closed, centre-mirrored path with a given paint. A style
/// chooses the paint (solid colour per band, or a per-column shader).</summary>
public interface IWaveformEnvelopePainter
{
    void Fill(SKCanvas canvas, float[] h, int binPx, int width, float midY, SKPaint paint);
}
