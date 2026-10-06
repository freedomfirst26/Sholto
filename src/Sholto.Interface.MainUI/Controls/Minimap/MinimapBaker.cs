using Avalonia.Media;
using Sholto.Data;
using Sholto.Interface.MainUI.Controls.WaveformStyles;
using Sholto.Interface.MainUI.Theming;
using SkiaSharp;

namespace Sholto.Interface.MainUI.Controls.Minimap;

public sealed class MinimapBaker(
    IMinimapPeakDownsampler downsampler,
    IMinimapGeometry geometry,
    IMinimapPhraseLines phraseLines,
    IMinimapSectionLabels labels) : IMinimapBaker
{
    // The section tint behind the wave, as a share of the section's colour.
    private const byte TintAlpha = 33;          // ~13 %
    private const byte DividerAlpha = 0xCC;     // 80 %
    private const byte LabelAlpha = 0xE6;       // 90 %
    private const float LabelSize = 8f;
    private const double LabelPadding = 3;      // DIPs each side of the text

    // A neutral section is a quiet tint of the label colour, not one of the kind colours.
    private const byte NeutralTintAlpha = 14;
    private const byte NeutralUnderlineAlpha = 0x55;

    // Phrase lines over the wave, in the label colour: faint every phrase, firmer every second, strong
    // every fourth. The strong line is twice as wide.
    private const byte ThinLineAlpha = 0x30;
    private const byte MediumLineAlpha = 0x60;
    private const byte StrongLineAlpha = 0x9C;

    private readonly IMinimapPeakDownsampler _downsampler = downsampler;
    private readonly IMinimapGeometry _geometry = geometry;
    private readonly IMinimapPhraseLines _phraseLines = phraseLines;
    private readonly IMinimapSectionLabels _labels = labels;

    public SKImage? Bake(
        WaveformPeaks peaks,
        MinimapStructure? structure,
        MinimapPalette palette,
        WaveformPalette waveform,
        IWaveformStyleStrategy style,
        int pixelWidth,
        int pixelHeight,
        double scale)
    {
        if (pixelWidth <= 0 || pixelHeight <= 0 || peaks.Min.Length == 0) return null;
        int rowH = (int)Math.Round(MinimapMetrics.LabelRowHeight * scale);
        int underline = Math.Max(1, (int)Math.Round(MinimapMetrics.UnderlineHeight * scale));
        int bodyH = pixelHeight - rowH;
        if (bodyH <= 0) return null;

        double trackSecs = peaks.Min.Length * peaks.SecondsPerPeak;

        // The wave first, so a style that cannot draw these peaks fails before any pixel is made.
        var squeezed = _downsampler.Downsample(peaks, pixelWidth);
        using var wave = style.Bake(squeezed, waveform with { Background = Colors.Transparent }, CancellationToken.None);

        var info = new SKImageInfo(pixelWidth, pixelHeight, SKColorType.Bgra8888, SKAlphaType.Premul);
        using var surface = SKSurface.Create(info);
        var canvas = surface.Canvas;
        canvas.Clear(palette.Backdrop.ToSk());

        using var fill = new SKPaint { Style = SKPaintStyle.Fill, IsAntialias = false };
        using var typeface = SKTypeface.FromFamilyName("Inter") ?? SKTypeface.Default;
        using var font = new SKFont(typeface, (float)(LabelSize * scale));
        using var text = new SKPaint { IsAntialias = true, Color = palette.Label.ToSk().WithAlpha(LabelAlpha) };

        bool placed = structure is { IsDrawable: true } && trackSecs > 0;
        if (placed)
        {
            foreach (var s in structure!.Sections)
            {
                float x0 = BarX(structure, s.StartBar, trackSecs, pixelWidth);
                float x1 = BarX(structure, s.EndBar, trackSecs, pixelWidth);
                if (x1 <= x0) continue;
                bool neutral = s.Kind == DeckSectionKind.Section;
                var colour = palette.For(s.Kind).ToSk();

                fill.Color = neutral ? colour.WithAlpha(NeutralUnderlineAlpha) : colour;   // underline
                canvas.DrawRect(x0, rowH - underline, x1 - x0, underline, fill);
                fill.Color = colour.WithAlpha(neutral ? NeutralTintAlpha : TintAlpha);     // tint behind the wave
                canvas.DrawRect(x0, rowH, x1 - x0, bodyH, fill);

                float room = (float)(x1 - x0 - 2 * LabelPadding * scale);
                string label = _labels.Fit(s.Kind, s.Bars, room, t => font.MeasureText(typeface.GetGlyphs(t)));
                if (label.Length > 0)
                {
                    float baseline = (float)((rowH - underline) / 2.0 + LabelSize * scale * 0.36);
                    canvas.DrawText(label, x0 + (float)(LabelPadding * scale), baseline, font, text);
                }
            }
        }

        if (wave is not null)
        {
            // One wave column per device pixel across, and a box-filtered squeeze of the style's fixed
            // 256-px height down the body. A filtered draw would do both at once through an isotropic
            // mipmap and blur the columns sideways by the vertical ratio.
            using var body = ShrinkRows(wave, bodyH);
            canvas.DrawImage(body, 0, rowH);
        }

        if (placed)
        {
            DrawPhraseLines(canvas, fill, structure!, palette, trackSecs, pixelWidth, pixelHeight, rowH, scale);

            fill.Color = palette.Backdrop.ToSk().WithAlpha(DividerAlpha);
            float lineW = Math.Max(1, (float)Math.Round(scale));
            foreach (var s in structure!.Sections)
            {
                float x = BarX(structure, s.StartBar, trackSecs, pixelWidth);
                if (x > 1) canvas.DrawRect(x, 0, lineW, pixelHeight, fill);
            }
        }
        return surface.Snapshot();
    }

    /// <summary>The x of a bar in device pixels, rounded to a whole pixel so blocks, dividers and phrase lines
    /// that share a bar share a column.</summary>
    private float BarX(MinimapStructure structure, int bar, double trackSecs, int pixelWidth) =>
        (float)Math.Round(_geometry.XOfBar(bar, structure.FirstDownbeatSec, structure.BarPeriodSec, trackSecs, pixelWidth));

    private void DrawPhraseLines(SKCanvas canvas, SKPaint fill, MinimapStructure structure, MinimapPalette palette,
        double trackSecs, int pixelWidth, int pixelHeight, int rowH, double scale)
    {
        int lastBar = (int)Math.Floor((trackSecs - structure.FirstDownbeatSec) / structure.BarPeriodSec);
        float thin = Math.Max(1, (float)Math.Round(scale));
        var label = palette.Label.ToSk();
        foreach (var line in _phraseLines.Lines(structure.PhraseGrid, lastBar))
        {
            float x = BarX(structure, line.Bar, trackSecs, pixelWidth);
            if (x <= 0 || x >= pixelWidth) continue;
            (byte alpha, float width) = line.Weight switch
            {
                MinimapPhraseLineWeight.Strong => (StrongLineAlpha, thin * 2),
                MinimapPhraseLineWeight.Medium => (MediumLineAlpha, thin),
                _ => (ThinLineAlpha, thin),
            };
            fill.Color = label.WithAlpha(alpha);
            canvas.DrawRect(x, rowH, width, pixelHeight - rowH, fill);
        }
    }

    /// <summary>The image with its height reduced to <paramref name="rows"/> by averaging the source rows
    /// each output row covers; the width is untouched.</summary>
    private SKImage ShrinkRows(SKImage source, int rows)
    {
        int w = source.Width, h = source.Height;
        var pixels = new byte[w * h * 4];
        var read = new SKImageInfo(w, h, SKColorType.Rgba8888, SKAlphaType.Premul);
        var handle = System.Runtime.InteropServices.GCHandle.Alloc(pixels, System.Runtime.InteropServices.GCHandleType.Pinned);
        try { source.ReadPixels(read, handle.AddrOfPinnedObject(), w * 4, 0, 0); }
        finally { handle.Free(); }

        var shrunk = new byte[w * rows * 4];
        var sum = new float[w * 4];
        for (int r = 0; r < rows; r++)
        {
            double y0 = r * (double)h / rows, y1 = (r + 1) * (double)h / rows;
            Array.Clear(sum);
            for (int j = (int)y0; j < Math.Min(h, (int)Math.Ceiling(y1)); j++)
            {
                float weight = (float)(Math.Min(y1, j + 1) - Math.Max(y0, j));
                int row = j * w * 4;
                for (int i = 0; i < sum.Length; i++) sum[i] += pixels[row + i] * weight;
            }
            float norm = (float)(y1 - y0);
            int dest = r * w * 4;
            for (int i = 0; i < sum.Length; i++) shrunk[dest + i] = (byte)Math.Clamp(sum[i] / norm + 0.5f, 0, 255);
        }
        return SKImage.FromPixelCopy(new SKImageInfo(w, rows, SKColorType.Rgba8888, SKAlphaType.Premul), shrunk);
    }
}
