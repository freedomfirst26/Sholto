using Sholto.Interface.MainUI.Theming;
using SkiaSharp;
using Sholto.Data;

namespace Sholto.Interface.MainUI.Controls.WaveformStyles;

/// <summary>Rekordbox "3-BAND": the bands drawn as nested layers, low (outer ring) ⊃ mid ⊃ high (core),
/// each in its own palette colour. The waveform Sholto has always drawn; the bake is moved unchanged
/// from <c>WaveformControl.BakeWaveform</c> (pre-C63) and pinned pixel-for-pixel by a test.</summary>
public sealed class ThreeBandWaveformStrategy(
    IWaveformEnvelopeBuilder envelopes,
    IWaveformEnvelopeRenderer renderer) : IWaveformStyleStrategy
{
    private readonly IWaveformEnvelopeBuilder _envelopes = envelopes;
    private readonly IWaveformEnvelopeRenderer _renderer = renderer;

    public string Id => "three-band";

    public string DisplayName => "3-BAND";

    public bool BakesSameColours(WaveformPalette baked, WaveformPalette next) =>
        (baked.Background, baked.Low, baked.Mid, baked.High) == (next.Background, next.Low, next.Mid, next.High);

    public SKImage? Bake(WaveformPeaks peaks, WaveformPalette palette, CancellationToken ct)
    {
        var env = _envelopes.Build(peaks, ct);
        if (env is null) return null;

        var info = new SKImageInfo(env.Width, env.Height, SKColorType.Rgba8888, SKAlphaType.Premul);
        using var surface = SKSurface.Create(info);
        var canvas = surface.Canvas;
        canvas.Clear(palette.Background.ToSk());

        // The three colours map to the three frequency band fields in
        // WaveformPeaks: Low (bass, outermost) / Mid / High (transients,
        // innermost). Contrast rule for every theme: no band may be so dark it
        // vanishes against the near-black background (the bass especially), and
        // adjacent bands must be separable by hue or luminance — otherwise the
        // waveform reads as one flat blob and you can't eyeball the arrangement.
        using var lowPaint  = new SKPaint { Color = palette.Low.ToSk(),  Style = SKPaintStyle.Fill, IsAntialias = true };
        using var midPaint  = new SKPaint { Color = palette.Mid.ToSk(),  Style = SKPaintStyle.Fill, IsAntialias = true };
        using var highPaint = new SKPaint { Color = palette.High.ToSk(), Style = SKPaintStyle.Fill, IsAntialias = true };

        if (env.Mono is { } amp)
        {
            _renderer.Fill(canvas, amp, env.BinPx, env.Width, env.MidY, lowPaint);
            return surface.Snapshot();
        }

        // Nested stack (Rekordbox 3-band): the bands are drawn CUMULATIVELY — white
        // innermost, orange around it, blue outermost — so blue always CONTAINS
        // orange contains white; a loud mid/high can never paint over the bass.
        // Cumulative edges: white core [0..high], orange out to [+mid], blue out to
        // [+low]. Draw outermost (blue) first so each inner band paints over the
        // centre and the outer bands survive as rings — blue ⊃ orange ⊃ white.
        int bins = env.Low.Length;
        float midY = env.MidY;
        var midE = new float[bins];
        var lowE = new float[bins];
        for (int b = 0; b < bins; b++)
        {
            midE[b] = MathF.Min(midY, env.High[b] + env.Mid[b]);
            lowE[b] = MathF.Min(midY, env.High[b] + env.Mid[b] + env.Low[b]);
        }

        _renderer.Fill(canvas, lowE,     env.BinPx, env.Width, midY, lowPaint);   // blue  — outer ring
        _renderer.Fill(canvas, midE,     env.BinPx, env.Width, midY, midPaint);   // orange — middle ring
        _renderer.Fill(canvas, env.High, env.BinPx, env.Width, midY, highPaint);  // white — core

        // Beat ticks + downbeat grid are drawn live by WaveformControl so they
        // keep scrolling even when this baked body is empty (all stems muted).
        return surface.Snapshot();
    }
}
