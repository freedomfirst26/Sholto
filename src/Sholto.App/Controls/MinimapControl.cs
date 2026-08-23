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

    /// <summary>Structural sections; when present they colour the strip (intro/build/
    /// drop/…). Falls back to energy-tier colours when null.</summary>
    public static readonly StyledProperty<SongSegments?> SegmentsProperty =
        AvaloniaProperty.Register<MinimapControl, SongSegments?>(nameof(Segments));

    /// <summary>Raised with a 0..1 fraction when the user clicks/drags to seek.</summary>
    public event Action<double>? Seeked;

    private SKImage? _baked;
    private WaveformPeaks? _bakedFor;

    static MinimapControl()
    {
        AffectsRender<MinimapControl>(PlayPositionProperty);
        PeaksProperty.Changed.AddClassHandler<MinimapControl>((c, _) => c.Rebake());
        SegmentsProperty.Changed.AddClassHandler<MinimapControl>((c, _) => c.Rebake());
    }

    public WaveformPeaks? Peaks
    {
        get => GetValue(PeaksProperty);
        set => SetValue(PeaksProperty, value);
    }

    public SongSegments? Segments
    {
        get => GetValue(SegmentsProperty);
        set => SetValue(SegmentsProperty, value);
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
        var segs = Segments;
        Task.Run(() =>
        {
            SKImage? img = null;
            try { img = Bake(snapshot, segs); }
            catch (Exception ex) { Console.WriteLine($"[Minimap] bake failed: {ex.Message}"); }
            Dispatcher.UIThread.Post(() =>
            {
                // NB: do NOT dispose the previous _baked here. The render thread may
                // still be blitting it inside a BlitOp captured on an earlier frame;
                // disposing its native SKImage out from under it is a use-after-free
                // (SIGSEGV). Let the finalizer reclaim the old image instead — same as
                // WaveformControl. Rapid track-switching is what triggered the crash.
                _baked = img;
                _bakedFor = snapshot;
                InvalidateVisual();
            });
        });
    }

    private static SKImage? Bake(WaveformPeaks p, SongSegments? segs)
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

        // Column x ↔ track seconds (same time domain as the segments' downbeats).
        double spp = p.SamplesPerPeak / (double)Sholto.Audio.AudioFileDecoder.TargetSampleRate;
        var segList = segs?.Segments;
        int segCursor = 0;

        for (int x = 0; x < w; x++)
        {
            float energy;
            if (hasBands)
            {
                var (nl, nm, nh) = scaling.Normalize(p.Low[x], p.Mid[x], p.High[x]);
                energy = Math.Clamp((nl + nm + nh) / (scaling.MaxTotal <= 0 ? 1 : scaling.MaxTotal), 0f, 1f);
            }
            else
            {
                energy = MathF.Max(MathF.Abs(p.Max[x]), MathF.Abs(p.Min[x]));
            }

            SKColor color;
            if (segList is { Count: > 0 })
            {
                double sec = x * spp;
                while (segCursor < segList.Count - 1 && sec >= segList[segCursor].EndSec) segCursor++;
                color = SegmentColor(segList[segCursor].Kind);
            }
            else
            {
                // No sections yet — fall back to energy tiers.
                color = energy < 0.33f ? new SKColor(0x5A, 0x66, 0x7C)
                      : energy < 0.66f ? new SKColor(0x3A, 0x86, 0xFF)
                      :                  new SKColor(0x3A, 0xEC, 0xFF);
            }

            // Baseline of at least 25% so even quiet sections show as a coloured lane.
            float barH = MathF.Max(h * 0.25f, energy * h);
            paint.Color = color;
            canvas.DrawLine(x, h - barH, x, h, paint);
        }

        // Thin dividers at section boundaries.
        if (segList is { Count: > 1 })
        {
            paint.Color = new SKColor(0x00, 0x00, 0x00, 0xB0);
            paint.StrokeWidth = 1;
            for (int i = 1; i < segList.Count; i++)
            {
                int x = (int)(segList[i].StartSec / spp);
                if (x > 0 && x < w) canvas.DrawLine(x, 0, x, h, paint);
            }
        }
        return surface.Snapshot();
    }

    // Section palette — distinct hues so the arrangement reads at a glance.
    private static SKColor SegmentColor(SegmentKind kind) => kind switch
    {
        SegmentKind.Intro     => new SKColor(0x5A, 0x66, 0x7C), // slate
        SegmentKind.BuildUp   => new SKColor(0xF0, 0xA0, 0x30), // amber (rising)
        SegmentKind.Drop      => new SKColor(0xFF, 0x5A, 0x2C), // hot orange (peak)
        SegmentKind.Breakdown => new SKColor(0x8A, 0x5C, 0xE0), // purple (atmospheric)
        SegmentKind.Verse     => new SKColor(0x3A, 0x86, 0xFF), // blue
        SegmentKind.Chorus    => new SKColor(0x3A, 0xEC, 0xFF), // cyan
        SegmentKind.Bridge    => new SKColor(0x2F, 0xB6, 0xA8), // teal
        SegmentKind.Outro     => new SKColor(0x4A, 0x54, 0x68), // dim slate
        _                     => new SKColor(0x5A, 0x66, 0x7C),
    };

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
                // Bottom divider so the strip reads as its own lane above the waveform.
                _head.Color = new SKColor(0x00, 0x00, 0x00, 0xC0); _head.StrokeWidth = 2;
                canvas.DrawLine(0, h - 1, w, h - 1, _head);
                _head.Color = SKColors.White;
                float x = (float)(_pos * w);
                canvas.DrawLine(x, 0, x, h, _head);
            }
        }
    }
}
