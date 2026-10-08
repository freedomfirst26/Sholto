using Sholto.Interface.MainUI.Theming;
using SkiaSharp;
using Sholto.Data;

namespace Sholto.Interface.MainUI.Controls.WaveformStyles;

/// <summary>Rekordbox "RGB": every source column is a solid 1-px vertical stroke, as tall as that
/// column's raw peak and coloured on its own by that column's spectrum, mirrored about the centre line.
///
/// Geometry: unlike 3-BAND there is no envelope — no bins, no attack/release follower. Columns are
/// drawn edge to edge (no gaps: a comb with gaps flickers as the deck scrolls it by fractions of a pixel
/// through the bilinear blit). The analyzer's own ±2-column box smoothing is the only smoothing. Height
/// follows amplitude relative to the track's loudest column through an expansion curve
/// (<see cref="HeightGamma"/>): a brickwalled master would otherwise read as a near-flat block, since
/// its peaks sit within a few percent of full scale everywhere.
///
/// Colour: each column's band energies are divided by that band's PER-BAND track reference
/// (<see cref="IWaveformBandScaler.Calibrate"/>, a high percentile per band), unclamped, and mixed by
/// <see cref="IRgbColourMixer"/>, which shows only each band's lead over the others, sharpened, so a
/// kick column is red beside a hat column blue even in a drop where every band sits near its reference.
/// Brightness then follows loudness (<see cref="BrightnessFloor"/>, <see cref="BrightnessGamma"/>): a
/// quiet intro is dim as well as short, the drop is both tall and vivid.
///
/// Only the bake allocates; the per-frame blit is the control's and is the same for every style.</summary>
public sealed class RgbWaveformStrategy(IWaveformBandScaler bandScaler, IRgbColourMixer mixer, int colourRadius) : IWaveformStyleStrategy
{
    private const int BakedHeight = 256;
    // How much of the half-height the loudest column reaches.
    private const float Fill = 0.95f;
    // Height = Fill · (amp / loudest)^HeightGamma: 2 spreads the top few dB of a loud master over the
    // upper half instead of flattening them into one block.
    private const float HeightGamma = 2f;
    // Columns quieter than this fraction of the loudest draw nothing (silence is empty, not a hairline).
    private const float Gate = 0.01f;
    // Brightness = BrightnessFloor + (1 - BrightnessFloor) · (amp / loudest)^BrightnessGamma.
    private const float BrightnessFloor = 0.35f;
    private const float BrightnessGamma = 0.6f;

    private readonly IWaveformBandScaler _bandScaler = bandScaler;
    private readonly IRgbColourMixer _mixer = mixer;
    private readonly int _colourRadius = colourRadius;

    public string Id => "rgb";

    public string DisplayName => "RGB";

    public bool BakesSameColours(WaveformPalette baked, WaveformPalette next) =>
        (baked.Background, baked.RgbLow, baked.RgbMid, baked.RgbHigh) == (next.Background, next.RgbLow, next.RgbMid, next.RgbHigh);

    public SKImage? Bake(WaveformPeaks peaks, WaveformPalette palette, CancellationToken ct)
    {
        int width = peaks.Min.Length;
        if (width == 0) return null;
        bool hasBands = peaks.Low.Length == width;
        int midY = BakedHeight / 2;

        float loudest = 1e-3f;
        for (int x = 0; x < width; x++) loudest = MathF.Max(loudest, Amplitude(peaks, x));

        var scaling = hasBands ? _bandScaler.Calibrate(peaks.Low, peaks.Mid, peaks.High) : default;
        SKColor lowC = palette.RgbLow.ToSk(), midC = palette.RgbMid.ToSk(), highC = palette.RgbHigh.ToSk();
        // No band data: one colour, the even mix of the three.
        SKColor mono = _mixer.Mix(1f, 1f, 1f, lowC, midC, highC);

        float[] sl = [], sm = [], sh = [];
        if (hasBands)
        {
            // Colour only is smoothed over ±radius columns; heights stay per column.
            var raw = (new float[width], new float[width], new float[width]);
            for (int x = 0; x < width; x++)
                (raw.Item1[x], raw.Item2[x], raw.Item3[x]) = scaling.NormalizeAbsolute(peaks.Low[x], peaks.Mid[x], peaks.High[x]);
            sl = BoxAverage(raw.Item1); sm = BoxAverage(raw.Item2); sh = BoxAverage(raw.Item3);
        }

        var info = new SKImageInfo(width, BakedHeight, SKColorType.Rgba8888, SKAlphaType.Premul);
        using var surface = SKSurface.Create(info);
        var canvas = surface.Canvas;
        canvas.Clear(palette.Background.ToSk());

        using var paint = new SKPaint { Style = SKPaintStyle.Fill, IsAntialias = false };
        for (int x = 0; x < width; x++)
        {
            if (ct.IsCancellationRequested) return null;
            float level = Amplitude(peaks, x) / loudest;
            if (level < Gate) continue;

            int half = Math.Clamp((int)MathF.Round(midY * Fill * MathF.Pow(level, HeightGamma)), 1, midY);
            SKColor colour = mono;
            if (hasBands)
            {
                colour = _mixer.Mix(sl[x], sm[x], sh[x], lowC, midC, highC);
            }
            paint.Color = Dim(colour, BrightnessFloor + (1f - BrightnessFloor) * MathF.Pow(level, BrightnessGamma));
            canvas.DrawRect(x, midY - half, 1, 2 * half, paint);
        }
        return surface.Snapshot();
    }

    private float[] BoxAverage(float[] src)
    {
        int n = src.Length;
        var prefix = new double[n + 1];
        for (int i = 0; i < n; i++) prefix[i + 1] = prefix[i] + src[i];
        var dst = new float[n];
        for (int i = 0; i < n; i++)
        {
            int lo = Math.Max(0, i - _colourRadius), hi = Math.Min(n - 1, i + _colourRadius);
            dst[i] = (float)((prefix[hi + 1] - prefix[lo]) / (hi - lo + 1));
        }
        return dst;
    }

    private float Amplitude(WaveformPeaks peaks, int x) =>
        MathF.Max(MathF.Abs(peaks.Max[x]), MathF.Abs(peaks.Min[x]));

    private SKColor Dim(SKColor c, float k) => new(
        (byte)MathF.Round(c.Red * k),
        (byte)MathF.Round(c.Green * k),
        (byte)MathF.Round(c.Blue * k),
        c.Alpha);
}
