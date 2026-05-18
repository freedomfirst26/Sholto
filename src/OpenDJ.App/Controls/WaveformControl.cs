using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;
using Avalonia.Rendering.SceneGraph;
using Avalonia.Skia;
using OpenDJ.Audio;
using SkiaSharp;

namespace OpenDJ.App.Controls;

public sealed class WaveformControl : Control
{
    public static readonly StyledProperty<WaveformPeaks?> PeaksProperty =
        AvaloniaProperty.Register<WaveformControl, WaveformPeaks?>(nameof(Peaks));

    public static readonly StyledProperty<double> PlayPositionProperty =
        AvaloniaProperty.Register<WaveformControl, double>(nameof(PlayPosition));

    static WaveformControl()
    {
        AffectsRender<WaveformControl>(PeaksProperty, PlayPositionProperty);
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

    public override void Render(DrawingContext context)
    {
        context.Custom(new WaveformDrawOperation(new Rect(Bounds.Size), Peaks, PlayPosition));
    }

    private sealed class WaveformDrawOperation : ICustomDrawOperation
    {
        private readonly WaveformPeaks? _peaks;
        private readonly double _playPosition;

        public WaveformDrawOperation(Rect bounds, WaveformPeaks? peaks, double playPosition)
        {
            Bounds = bounds;
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
            int width = (int)Bounds.Width;
            int height = (int)Bounds.Height;

            canvas.Clear(new SKColor(0x11, 0x11, 0x11));

            var peaks = _peaks;
            if (peaks is null || peaks.Min.Length == 0) return;

            int totalPeaks = peaks.Min.Length;
            int centerPeak = (int)(_playPosition * totalPeaks);
            int half = width / 2;
            float midY = height / 2f;

            using var playedPaint = new SKPaint
            {
                Color = new SKColor(0x00, 0xFF, 0xCC, 0xCC),
                StrokeWidth = 1,
                IsAntialias = false
            };
            using var unplayedPaint = new SKPaint
            {
                Color = new SKColor(0x00, 0x88, 0x66, 0xCC),
                StrokeWidth = 1,
                IsAntialias = false
            };

            for (int x = 0; x < width; x++)
            {
                int peakIdx = centerPeak - half + x;
                if (peakIdx < 0 || peakIdx >= totalPeaks) continue;

                float top    = midY - (peaks.Max[peakIdx] * midY);
                float bottom = midY - (peaks.Min[peakIdx] * midY);
                var paint = peakIdx < centerPeak ? playedPaint : unplayedPaint;
                canvas.DrawLine(x, top, x, bottom, paint);
            }

            using var headPaint = new SKPaint { Color = SKColors.White, StrokeWidth = 2 };
            canvas.DrawLine(half, 0, half, height, headPaint);
        }
    }
}
