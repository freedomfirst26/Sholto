using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Media;
using Avalonia.Platform;
using Avalonia.Rendering.SceneGraph;
using Avalonia.Skia;
using Avalonia.Threading;
using Sholto.Analysis;
using SkiaSharp;

namespace Sholto.App.Controls;

/// <summary>
/// A compressed full-song overview strip above the scrolling waveform. Shows the
/// whole track at once, coloured by per-column energy so the arrangement (quiet
/// intro, build, drop, breakdown, outro) is readable at a glance, with a playhead
/// and click/drag-to-seek so you can jump straight to a section.
///
/// The energy strip is baked to an SKImage once per track (like WaveformControl);
/// per frame we just blit it and draw the playhead — cheap even at 60 Hz.
/// </summary>
public sealed class MinimapControl : Control
{
    private const int BakedHeight = 64;
    private static readonly SKColor Bg = new(0x11, 0x11, 0x11);

    public static readonly StyledProperty<WaveformPeaks?> PeaksProperty =
        AvaloniaProperty.Register<MinimapControl, WaveformPeaks?>(nameof(Peaks));

    public static readonly StyledProperty<double> PlayPositionProperty =
        AvaloniaProperty.Register<MinimapControl, double>(nameof(PlayPosition));

    /// <summary>Raised with a 0..1 fraction when the user clicks/drags to seek.</summary>
    public event Action<double>? Seeked;

    private SKImage? _baked;
    private WaveformPeaks? _bakedFor;

    static MinimapControl()
    {
        AffectsRender<MinimapControl>(PlayPositionProperty);
        PeaksProperty.Changed.AddClassHandler<MinimapControl>((c, _) => c.Rebake());
    }

    public WaveformPeaks? Peaks
    {
        get => GetValue(PeaksProperty);
        set => SetValue(PeaksProperty, value);
    }

    public double PlayPosition
    {
        get => GetValue(PlayPositionProperty);
        set => SetValue(PlayPositionProperty, value);
    }

    private void Rebake()
    {
        var peaks = Peaks;
        if (peaks is null || peaks.Min.Length == 0)
        {
            _baked = null; _bakedFor = null; InvalidateVisual(); return;
        }
        var snapshot = peaks;
        Task.Run(() =>
        {
            var img = Bake(snapshot);
            Dispatcher.UIThread.Post(() =>
            {
                _baked?.Dispose();
                _baked = img;
                _bakedFor = snapshot;
                InvalidateVisual();
            });
        });
    }

    private static SKImage? Bake(WaveformPeaks p)
    {
        int w = p.Min.Length;
        if (w == 0) return null;
        int h = BakedHeight;
        var info = new SKImageInfo(w, h, SKColorType.Rgba8888, SKAlphaType.Premul);
        using var surface = SKSurface.Create(info);
        var canvas = surface.Canvas;
        canvas.Clear(Bg);

        bool hasBands = p.Low.Length == w;
        var scaling = hasBands ? WaveformBandScaling.Calibrate(p.Low, p.Mid, p.High) : default;
        using var paint = new SKPaint { IsAntialias = false, StrokeWidth = 1 };

        for (int x = 0; x < w; x++)
        {
            float energy;
            SKColor color;
            if (hasBands)
            {
                var (nl, nm, nh) = scaling.Normalize(p.Low[x], p.Mid[x], p.High[x]);
                energy = Math.Clamp((nl + nm + nh) / (scaling.MaxTotal <= 0 ? 1 : scaling.MaxTotal), 0f, 1f);
                // Colour by energy tier so sections read: quiet=slate, mid=blue, peak=cyan.
                color = energy < 0.33f ? new SKColor(0x3A, 0x44, 0x55)
                      : energy < 0.66f ? new SKColor(0x2E, 0x6B, 0xE0)
                      :                  new SKColor(0x34, 0xE0, 0xF0);
            }
            else
            {
                energy = MathF.Max(MathF.Abs(p.Max[x]), MathF.Abs(p.Min[x]));
                color = new SKColor(0x3A, 0x44, 0x55);
            }
            float barH = MathF.Max(1f, energy * h);
            paint.Color = color;
            canvas.DrawLine(x, h - barH, x, h, paint);
        }
        return surface.Snapshot();
    }

    public override void Render(DrawingContext context)
    {
        context.Custom(new BlitOp(new Rect(Bounds.Size), _baked, PlayPosition));
    }

    protected override void OnPointerPressed(PointerPressedEventArgs e)
    {
        base.OnPointerPressed(e);
        SeekAt(e.GetPosition(this).X);
        e.Pointer.Capture(this);
        e.Handled = true;
    }

    protected override void OnPointerMoved(PointerEventArgs e)
    {
        base.OnPointerMoved(e);
        if (ReferenceEquals(e.Pointer.Captured, this))
            SeekAt(e.GetPosition(this).X);
    }

    private void SeekAt(double x)
    {
        double w = Bounds.Width;
        if (w <= 0) return;
        Seeked?.Invoke(Math.Clamp(x / w, 0.0, 1.0));
    }

    private sealed class BlitOp : ICustomDrawOperation
    {
        [ThreadStatic] private static SKPaint? _blit;
        [ThreadStatic] private static SKPaint? _head;
        private readonly SKImage? _image;
        private readonly double _pos;

        public BlitOp(Rect bounds, SKImage? image, double pos) { Bounds = bounds; _image = image; _pos = pos; }
        public Rect Bounds { get; }
        public bool HitTest(Point p) => Bounds.Contains(p);
        public bool Equals(ICustomDrawOperation? other) => false;
        public void Dispose() { }

        public void Render(ImmediateDrawingContext context)
        {
            var lease = ((ISkiaSharpApiLeaseFeature?)context.TryGetFeature(typeof(ISkiaSharpApiLeaseFeature)))?.Lease();
            if (lease is null) return;
            using (lease)
            {
                var canvas = lease.SkCanvas;
                int w = (int)Bounds.Width, h = (int)Bounds.Height;
                canvas.Clear(Bg);
                if (_image is not null)
                {
                    _blit ??= new SKPaint { FilterQuality = SKFilterQuality.Low };
                    canvas.DrawImage(_image, new SKRect(0, 0, _image.Width, _image.Height), new SKRect(0, 0, w, h), _blit);
                }
                _head ??= new SKPaint { Color = SKColors.White, StrokeWidth = 2, IsAntialias = false };
                float x = (float)(_pos * w);
                canvas.DrawLine(x, 0, x, h, _head);
            }
        }
    }
}
