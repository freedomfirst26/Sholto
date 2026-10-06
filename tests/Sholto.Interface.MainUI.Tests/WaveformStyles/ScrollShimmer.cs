using Sholto.Interface.MainUI.Controls;
using SkiaSharp;

namespace Sholto.Interface.MainUI.Tests.WaveformStyles;

/// <summary>Measures how much a baked waveform's look changes from frame to frame as the deck scrolls
/// it, the way <c>WaveformControl.BlitOperation</c> does: the source window moves 46.875 columns/s
/// (≈ 0.78 columns per 60 Hz frame at unity tempo), is drawn with the control's bilinear blit paint onto
/// a canvas at the display's device scale and, when <c>snap</c> is on, from the position
/// <see cref="WaveformScrollSnap"/> gives. Per frame it sums the luminance steps between neighbouring
/// device pixels over rows across the upper half (the centre row sits inside every column's body and
/// inside 3-BAND's solid core, so it alone sees no edges); a stable image gives nearly the same sum every
/// frame, a 1-px comb swings between full contrast and a half-bright smear as its phase crosses the
/// pixel grid.</summary>
internal sealed class ScrollShimmer
{
    private const int Width = 400, Height = 100, Frames = 16;
    private const double ColumnsPerFrame = 0.78;
    private const double StartColumn = 300;

    private readonly WaveformScrollSnap _snap = new();

    public (double Min, double Max) Measure(SKImage image, double deviceScale = 1, bool snap = false)
    {
        int w = (int)Math.Ceiling(Width * deviceScale), h = (int)Math.Ceiling(Height * deviceScale);
        using var surface = SKSurface.Create(new SKImageInfo(w, h, SKColorType.Rgba8888, SKAlphaType.Premul));
        using var paint = new SKPaint { FilterQuality = SKFilterQuality.Low };
        using var frame = new SKBitmap(w, h, SKColorType.Rgba8888, SKAlphaType.Premul);
        double min = double.MaxValue, max = 0;
        for (int k = 0; k < Frames; k++)
        {
            double left = StartColumn + k * ColumnsPerFrame;
            if (snap) left = _snap.SnapLeft(left, deviceScale, originDeviceX: 0);
            var canvas = surface.Canvas;
            canvas.ResetMatrix();
            canvas.Clear(SKColors.Black);
            canvas.Scale((float)deviceScale);
            canvas.DrawImage(image, new SKRect((float)left, 0, (float)(left + Width), image.Height), new SKRect(0, 0, Width, Height), paint);
            using var snapshot = surface.Snapshot();
            snapshot.ReadPixels(frame.Info, frame.GetPixels(), frame.RowBytes, 0, 0);
            double steps = 0;
            for (int y = (int)(10 * deviceScale); y <= h / 2; y += (int)(10 * deviceScale))
                for (int x = 0; x + 1 < w; x++)
                    steps += Math.Abs(Luma(frame.GetPixel(x, y)) - Luma(frame.GetPixel(x + 1, y)));
            min = Math.Min(min, steps);
            max = Math.Max(max, steps);
        }
        return (min, max);
    }

    private double Luma(SKColor c) => 0.2126 * c.Red + 0.7152 * c.Green + 0.0722 * c.Blue;
}
