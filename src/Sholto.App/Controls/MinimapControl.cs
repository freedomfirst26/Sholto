using System.Globalization;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Media;
using Sholto.Analysis;

namespace Sholto.App.Controls;

/// <summary>
/// Stationary whole-song section map: one rectangle per section, coloured by section
/// type, with opacity = the section's power (a calm intro is see-through; the drop is
/// full colour). Click/drag to jump to any section.
///
/// Drawn with plain Avalonia primitives (DrawingContext) rather than a SkiaSharp
/// custom draw operation — the custom op refused to composite in this layout even
/// when it rendered with a valid image, so we draw rectangles directly, which is both
/// reliable and exactly the intended look.
/// </summary>
public sealed class MinimapControl : Control
{
    private const double StripHeight = 42;

    public static readonly StyledProperty<SongSegments?> SegmentsProperty =
        AvaloniaProperty.Register<MinimapControl, SongSegments?>(nameof(Segments));

    public static readonly StyledProperty<WaveformPeaks?> PeaksProperty =
        AvaloniaProperty.Register<MinimapControl, WaveformPeaks?>(nameof(Peaks));

    public static readonly StyledProperty<double> PlayPositionProperty =
        AvaloniaProperty.Register<MinimapControl, double>(nameof(PlayPosition));

    /// <summary>Raised with a 0..1 fraction when the user clicks/drags to seek.</summary>
    public event Action<double>? Seeked;

    // Precomputed section blocks (fractions of the track) — rebuilt when the
    // segments/peaks change; Render just paints them + the playhead.
    private readonly List<Block> _blocks = new();
    private static readonly Typeface LabelTypeface = new("Inter, sans-serif");

    static MinimapControl()
    {
        AffectsRender<MinimapControl>(PlayPositionProperty);
        SegmentsProperty.Changed.AddClassHandler<MinimapControl>((c, _) => c.Rebuild());
        PeaksProperty.Changed.AddClassHandler<MinimapControl>((c, _) => c.Rebuild());
    }

    public SongSegments? Segments { get => GetValue(SegmentsProperty); set => SetValue(SegmentsProperty, value); }
    public WaveformPeaks? Peaks { get => GetValue(PeaksProperty); set => SetValue(PeaksProperty, value); }
    public double PlayPosition { get => GetValue(PlayPositionProperty); set => SetValue(PlayPositionProperty, value); }

    private readonly record struct Block(double Start, double End, Color Color, double Opacity, string Label);

    private void Rebuild()
    {
        _blocks.Clear();
        var segs = Segments?.Segments;
        var pk = Peaks;
        if (segs is not { Count: > 0 } || pk is not { Min.Length: > 0 }) { InvalidateVisual(); return; }

        double spp = pk.SamplesPerPeak / (double)Sholto.Audio.AudioFileDecoder.TargetSampleRate;
        double trackSecs = pk.Min.Length * spp;
        if (trackSecs <= 0) { InvalidateVisual(); return; }

        bool hasBands = pk.Low.Length == pk.Min.Length;
        var scaling = hasBands ? WaveformBandScaling.Calibrate(pk.Low, pk.Mid, pk.High) : default;

        foreach (var s in segs)
        {
            // Power = mean band energy across the section's columns (0..1).
            double power = 0;
            if (hasBands)
            {
                int c0 = Math.Clamp((int)(s.StartSec / spp), 0, pk.Min.Length);
                int c1 = Math.Clamp((int)(s.EndSec / spp), c0 + 1, pk.Min.Length);
                double sum = 0;
                for (int c = c0; c < c1; c++)
                {
                    var (nl, nm, nh) = scaling.Normalize(pk.Low[c], pk.Mid[c], pk.High[c]);
                    sum += (nl + nm + nh) / (scaling.MaxTotal <= 0 ? 1 : scaling.MaxTotal);
                }
                power = c1 > c0 ? Math.Clamp(sum / (c1 - c0), 0, 1) : 0;
            }
            // Calm = see-through, intense = full colour.
            double opacity = 0.45 + 0.55 * power;
            _blocks.Add(new Block(s.StartSec / trackSecs, s.EndSec / trackSecs,
                ColorFor(s.Kind), opacity, LabelFor(s.Kind)));
        }
        InvalidateVisual();
    }

    protected override Size MeasureOverride(Size availableSize)
    {
        double w = double.IsInfinity(availableSize.Width) ? 0 : availableSize.Width;
        return new Size(w, StripHeight);
    }

    public override void Render(DrawingContext context)
    {
        double w = Bounds.Width, h = Bounds.Height;
        if (w <= 0 || h <= 0) return;

        // Backdrop so the strip reads as its own lane.
        context.FillRectangle(new SolidColorBrush(Color.FromRgb(0x11, 0x12, 0x16)), new Rect(0, 0, w, h));

        foreach (var b in _blocks)
        {
            double x = b.Start * w;
            double bw = Math.Max(1, (b.End - b.Start) * w);
            var brush = new SolidColorBrush(b.Color, b.Opacity);
            context.FillRectangle(brush, new Rect(x, 0, bw, h));

            if (b.Label.Length > 0 && bw >= 30)
            {
                var ft = new FormattedText(b.Label, CultureInfo.InvariantCulture,
                    FlowDirection.LeftToRight, LabelTypeface, 9,
                    new SolidColorBrush(Color.FromArgb(0xF0, 0xFF, 0xFF, 0xFF)));
                context.DrawText(ft, new Point(x + 4, 3));
            }
        }

        // Section dividers.
        var divider = new Pen(new SolidColorBrush(Color.FromArgb(0xB0, 0, 0, 0)), 1);
        foreach (var b in _blocks)
        {
            double x = b.Start * w;
            if (x > 0.5) context.DrawLine(divider, new Point(x, 0), new Point(x, h));
        }

        // Playhead.
        double px = Math.Clamp(PlayPosition, 0, 1) * w;
        context.DrawLine(new Pen(Brushes.White, 2), new Point(px, 0), new Point(px, h));
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
        if (ReferenceEquals(e.Pointer.Captured, this)) SeekAt(e.GetPosition(this).X);
    }

    private void SeekAt(double x)
    {
        if (Bounds.Width <= 0) return;
        Seeked?.Invoke(Math.Clamp(x / Bounds.Width, 0.0, 1.0));
    }

    // Distinct hue per section type so the arrangement reads at a glance.
    private static Color ColorFor(SegmentKind k) => k switch
    {
        SegmentKind.Intro     => Color.FromRgb(0x5A, 0x66, 0x7C),
        SegmentKind.BuildUp   => Color.FromRgb(0xF0, 0xA0, 0x30),
        SegmentKind.Drop      => Color.FromRgb(0xFF, 0x5A, 0x2C),
        SegmentKind.Breakdown => Color.FromRgb(0x8A, 0x5C, 0xE0),
        SegmentKind.Verse     => Color.FromRgb(0x3A, 0x86, 0xFF),
        SegmentKind.Chorus    => Color.FromRgb(0x3A, 0xEC, 0xFF),
        SegmentKind.Bridge    => Color.FromRgb(0x2F, 0xB6, 0xA8),
        SegmentKind.Outro     => Color.FromRgb(0x4A, 0x54, 0x68),
        _                     => Color.FromRgb(0x5A, 0x66, 0x7C),
    };

    private static string LabelFor(SegmentKind k) => k switch
    {
        SegmentKind.Intro => "INTRO",   SegmentKind.BuildUp => "BUILD",
        SegmentKind.Drop => "DROP",     SegmentKind.Breakdown => "BREAK",
        SegmentKind.Verse => "VERSE",   SegmentKind.Chorus => "CHORUS",
        SegmentKind.Bridge => "BRIDGE", SegmentKind.Outro => "OUTRO",
        _ => "",
    };
}
