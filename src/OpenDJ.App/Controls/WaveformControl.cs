using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;
using Avalonia.Rendering.SceneGraph;
using Avalonia.Skia;
using Avalonia.Threading;
using OpenDJ.Audio;
using OpenDJ.Audio.Analysis;
using SkiaSharp;

namespace OpenDJ.App.Controls;

public enum WaveformPalette { Bands, Hot, Plasma }

/// <summary>
/// Pre-renders the entire waveform to an offscreen SKImage at track load,
/// then blits a window per frame. One textured rectangle per frame instead of
/// thousands of DrawLine calls.
/// </summary>
public sealed class WaveformControl : Control
{
    private const int BakedHeight = 256;
    private static readonly SKColor BgColor = new(0x11, 0x11, 0x11);

    public static readonly StyledProperty<WaveformPeaks?> PeaksProperty =
        AvaloniaProperty.Register<WaveformControl, WaveformPeaks?>(nameof(Peaks));

    public static readonly StyledProperty<double[]?> BeatTimesProperty =
        AvaloniaProperty.Register<WaveformControl, double[]?>(nameof(BeatTimes));

    public static readonly StyledProperty<double[]?> DownbeatTimesProperty =
        AvaloniaProperty.Register<WaveformControl, double[]?>(nameof(DownbeatTimes));

    public static readonly StyledProperty<double> PlayPositionProperty =
        AvaloniaProperty.Register<WaveformControl, double>(nameof(PlayPosition));

    public static readonly StyledProperty<WaveformPalette> PaletteProperty =
        AvaloniaProperty.Register<WaveformControl, WaveformPalette>(nameof(Palette), WaveformPalette.Bands);

    private SKImage? _baked;
    private WaveformPeaks? _bakedFor;
    private WaveformPalette _bakedPalette;
    private CancellationTokenSource? _bakeCts;

    static WaveformControl()
    {
        AffectsRender<WaveformControl>(PlayPositionProperty);
        PeaksProperty.Changed.AddClassHandler<WaveformControl>((c, _) => c.Rebake());
        BeatTimesProperty.Changed.AddClassHandler<WaveformControl>((c, _) => c.Rebake());
        DownbeatTimesProperty.Changed.AddClassHandler<WaveformControl>((c, _) => c.Rebake());
        PaletteProperty.Changed.AddClassHandler<WaveformControl>((c, _) => c.Rebake());
    }

    public WaveformPeaks? Peaks
    {
        get => GetValue(PeaksProperty);
        set => SetValue(PeaksProperty, value);
    }

    public double[]? BeatTimes
    {
        get => GetValue(BeatTimesProperty);
        set => SetValue(BeatTimesProperty, value);
    }

    public double[]? DownbeatTimes
    {
        get => GetValue(DownbeatTimesProperty);
        set => SetValue(DownbeatTimesProperty, value);
    }

    public double PlayPosition
    {
        get => GetValue(PlayPositionProperty);
        set => SetValue(PlayPositionProperty, value);
    }

    public WaveformPalette Palette
    {
        get => GetValue(PaletteProperty);
        set => SetValue(PaletteProperty, value);
    }

    private void Rebake()
    {
        var peaks = Peaks;
        _bakeCts?.Cancel();
        if (peaks is null || peaks.Min.Length == 0)
        {
            _baked = null;
            _bakedFor = null;
            InvalidateVisual();
            return;
        }

        var cts = new CancellationTokenSource();
        _bakeCts = cts;
        var snapshot = peaks;
        var palette = Palette;
        var beats = BeatTimes ?? [];
        var downbeats = DownbeatTimes ?? [];
        Task.Run(() =>
        {
            var img = BakeWaveform(snapshot, beats, downbeats, palette, cts.Token);
            if (cts.IsCancellationRequested || img is null) { img?.Dispose(); return; }
            Dispatcher.UIThread.Post(() =>
            {
                if (cts.IsCancellationRequested) { img.Dispose(); return; }
                _baked = img;
                _bakedFor = snapshot;
                _bakedPalette = palette;
                InvalidateVisual();
            });
        });
    }

    private static SKImage? BakeWaveform(WaveformPeaks peaks, double[] beatTimes, double[] downbeatTimes, WaveformPalette palette, CancellationToken ct)
    {
        int width = peaks.Min.Length;
        if (width == 0) return null;
        int height = BakedHeight;
        float midY = height / 2f;

        var info = new SKImageInfo(width, height, SKColorType.Rgba8888, SKAlphaType.Premul);
        using var surface = SKSurface.Create(info);
        var canvas = surface.Canvas;
        canvas.Clear(BgColor);

        // Bands: authentic Rekordbox — blue / white / yellow
        // Hot:   Serato — red / green / blue
        // Plasma: OpenDJ 2026 — violet / hot-pink / mint
        var (lowColor, midColor, highColor) = palette switch
        {
            WaveformPalette.Hot    => (new SKColor(0xFF, 0x3D, 0x3D), new SKColor(0x3D, 0xFF, 0x7A), new SKColor(0x3D, 0x8B, 0xFF)),
            WaveformPalette.Plasma => (new SKColor(0x7C, 0x5C, 0xFF), new SKColor(0xFF, 0x4E, 0x9A), new SKColor(0x34, 0xF0, 0xC6)),
            _                      => (new SKColor(0x1E, 0x59, 0xFF), new SKColor(0xFF, 0xFF, 0xFF), new SKColor(0xFF, 0xC7, 0x00)),
        };
        using var lowPaint  = new SKPaint { Color = lowColor, StrokeWidth = 1, IsAntialias = false };
        using var midPaint  = new SKPaint { Color = midColor, StrokeWidth = 1, IsAntialias = false };
        using var highPaint = new SKPaint { Color = highColor, StrokeWidth = 1, IsAntialias = false };

        bool hasBands = peaks.Low.Length == width;

        for (int x = 0; x < width; x++)
        {
            if (ct.IsCancellationRequested) return null;

            float amp = MathF.Max(MathF.Abs(peaks.Max[x]), MathF.Abs(peaks.Min[x]));
            float barHeight = amp * midY;
            if (barHeight < 1f) continue;

            if (!hasBands)
            {
                canvas.DrawLine(x, midY - barHeight, x, midY + barHeight, lowPaint);
                continue;
            }

            float l = peaks.Low[x];
            float m = peaks.Mid[x];
            float h = peaks.High[x];
            float sum = l + m + h;
            if (sum <= 1e-5f) continue;

            float hSeg = barHeight * (h / sum);
            float mSeg = barHeight * (m / sum);
            float lSeg = barHeight - hSeg - mSeg;

            float lowEdge  = lSeg;
            float midEdge  = lowEdge + mSeg;
            float highEdge = midEdge + hSeg;

            canvas.DrawLine(x, midY,           x, midY + lowEdge,  lowPaint);
            canvas.DrawLine(x, midY + lowEdge, x, midY + midEdge,  midPaint);
            canvas.DrawLine(x, midY + midEdge, x, midY + highEdge, highPaint);

            canvas.DrawLine(x, midY,           x, midY - lowEdge,  lowPaint);
            canvas.DrawLine(x, midY - lowEdge, x, midY - midEdge,  midPaint);
            canvas.DrawLine(x, midY - midEdge, x, midY - highEdge, highPaint);
        }

        // Beat ticks at the top edge. Real downbeats from madmom if available,
        // otherwise fall back to "every 4th beat".
        if (beatTimes.Length > 0)
        {
            using var tickPaint     = new SKPaint { Color = new SKColor(0xFF, 0xFF, 0xFF, 0xC0), StrokeWidth = 1, IsAntialias = false };
            using var downbeatPaint = new SKPaint { Color = new SKColor(0xFF, 0xCC, 0x00), StrokeWidth = 2, IsAntialias = false };
            double secondsPerPeak = peaks.SamplesPerPeak / 44100.0;

            // Build a HashSet of downbeat column indices for fast lookup.
            HashSet<int>? downbeatCols = null;
            if (downbeatTimes.Length > 0)
            {
                downbeatCols = new HashSet<int>();
                foreach (var t in downbeatTimes)
                    downbeatCols.Add((int)Math.Round(t / secondsPerPeak));
            }

            for (int i = 0; i < beatTimes.Length; i++)
            {
                int col = (int)Math.Round(beatTimes[i] / secondsPerPeak);
                if (col < 0 || col >= width) continue;
                bool downbeat = downbeatCols is not null
                    ? downbeatCols.Contains(col)
                    : (i % 4) == 0;
                int tickH = downbeat ? 10 : 5;
                canvas.DrawLine(col, 0, col, tickH, downbeat ? downbeatPaint : tickPaint);
            }
        }

        return surface.Snapshot();
    }

    public override void Render(DrawingContext context)
    {
        context.Custom(new BlitOperation(new Rect(Bounds.Size), _baked, _bakedFor, PlayPosition));
    }

    private sealed class BlitOperation : ICustomDrawOperation
    {
        private readonly SKImage? _image;
        private readonly WaveformPeaks? _peaks;
        private readonly double _playPosition;

        public BlitOperation(Rect bounds, SKImage? image, WaveformPeaks? peaks, double playPosition)
        {
            Bounds = bounds;
            _image = image;
            _peaks = peaks;
            _playPosition = playPosition;
        }

        public Rect Bounds { get; }
        public bool HitTest(Point p) => Bounds.Contains(p);
        public bool Equals(ICustomDrawOperation? other) => false;
        public void Dispose() { }

        public void Render(ImmediateDrawingContext context)
        {
            var leaseFeature = (ISkiaSharpApiLeaseFeature?)context.TryGetFeature(typeof(ISkiaSharpApiLeaseFeature));
            if (leaseFeature is null) return;
            using var lease = leaseFeature.Lease();
            var canvas = lease.SkCanvas;

            int dstW = (int)Bounds.Width;
            int dstH = (int)Bounds.Height;
            canvas.Clear(BgColor);

            if (_image is not null && _peaks is not null && _peaks.Min.Length > 0)
            {
                int totalPeaks = _peaks.Min.Length;
                float centerPeak = (float)(_playPosition * totalPeaks); // keep sub-pixel precision
                float half = dstW / 2f;

                float srcXStart = centerPeak - half;
                float srcXEnd   = centerPeak + half;

                float clipLeft  = srcXStart < 0 ? -srcXStart : 0;
                float clipRight = srcXEnd > totalPeaks ? srcXEnd - totalPeaks : 0;

                float validSrcW = (srcXEnd - srcXStart) - clipLeft - clipRight;
                if (validSrcW > 0)
                {
                    var src = new SKRect(srcXStart + clipLeft, 0, srcXEnd - clipRight, _image.Height);
                    var dst = new SKRect(clipLeft, 0, dstW - clipRight, dstH);
                    // Bilinear filtering — smooths the sub-pixel scroll between
                    // columns each frame so the waveform glides instead of stepping.
                    using var paint = new SKPaint { FilterQuality = SKFilterQuality.Low };
                    canvas.DrawImage(_image, src, dst, paint);
                }
            }

            using var headPaint = new SKPaint { Color = SKColors.White, StrokeWidth = 2 };
            int halfX = dstW / 2;
            canvas.DrawLine(halfX, 0, halfX, dstH, headPaint);
        }
    }
}
