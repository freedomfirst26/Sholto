using System.Globalization;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;
using Sholto.Analysis;
using Sholto.App.Theming;

namespace Sholto.App.Controls;

/// <summary>
/// Stationary whole-song section map: one rectangle per section, coloured by section
/// type, with opacity = the section's power (a calm intro is see-through; the drop is
/// full colour). Display-only — transport is controller-only (platter, CUE, Shift + CUE).
///
/// Drawn with plain Avalonia primitives (DrawingContext) rather than a SkiaSharp
/// custom draw operation — the custom op refused to composite in this layout even
/// when it rendered with a valid image, so we draw rectangles directly, which is both
/// reliable and exactly the intended look.
/// </summary>
public sealed class MinimapControl : Control
{
    private const double StripHeight = 24;

    // Fallback so the designer/preview (no theme resource resolved yet) doesn't crash.
    private static readonly MinimapPalette DefaultPalette = Themes.Classic.Minimap;

    public static readonly StyledProperty<SongSegments?> SegmentsProperty =
        AvaloniaProperty.Register<MinimapControl, SongSegments?>(nameof(Segments));

    public static readonly StyledProperty<WaveformPeaks?> PeaksProperty =
        AvaloniaProperty.Register<MinimapControl, WaveformPeaks?>(nameof(Peaks));

    public static readonly StyledProperty<double> PlayPositionProperty =
        AvaloniaProperty.Register<MinimapControl, double>(nameof(PlayPosition));

    public static readonly StyledProperty<MinimapPalette?> PaletteProperty =
        AvaloniaProperty.Register<MinimapControl, MinimapPalette?>(nameof(Palette));

    // Precomputed section blocks (fractions of the track) — rebuilt when the
    // segments/peaks change; Render just paints them + the playhead.
    private readonly List<Block> _blocks = new();
    private static readonly Typeface LabelTypeface = new("Inter, sans-serif");

    static MinimapControl()
    {
        AffectsRender<MinimapControl>(PlayPositionProperty);
        AffectsRender<MinimapControl>(PaletteProperty);
        SegmentsProperty.Changed.AddClassHandler<MinimapControl>((c, _) => c.Rebuild());
        PeaksProperty.Changed.AddClassHandler<MinimapControl>((c, _) => c.Rebuild());
    }

    public MinimapControl()
    {
        // Display-only: transport is controller-only, so clicks fall through
        // and no hand cursor appears over the strip.
        IsHitTestVisible = false;
    }

    public SongSegments? Segments { get => GetValue(SegmentsProperty); set => SetValue(SegmentsProperty, value); }
    public WaveformPeaks? Peaks { get => GetValue(PeaksProperty); set => SetValue(PeaksProperty, value); }
    public double PlayPosition { get => GetValue(PlayPositionProperty); set => SetValue(PlayPositionProperty, value); }
    public MinimapPalette? Palette { get => GetValue(PaletteProperty); set => SetValue(PaletteProperty, value); }

    private readonly record struct Block(double Start, double End, SegmentKind Kind, double Opacity, string Label);

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
                s.Kind, opacity, LabelFor(s.Kind)));
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

        var palette = Palette ?? DefaultPalette;

        // Backdrop so the strip reads as its own lane.
        context.FillRectangle(new SolidColorBrush(palette.Backdrop), new Rect(0, 0, w, h));

        var labelBrush = new SolidColorBrush(palette.Label);
        foreach (var b in _blocks)
        {
            double x = b.Start * w;
            double bw = Math.Max(1, (b.End - b.Start) * w);
            var brush = new SolidColorBrush(palette.For(b.Kind), b.Opacity);
            context.FillRectangle(brush, new Rect(x, 0, bw, h));

            if (b.Label.Length > 0 && bw >= 30)
            {
                var ft = new FormattedText(b.Label, CultureInfo.InvariantCulture,
                    FlowDirection.LeftToRight, LabelTypeface, 8, labelBrush);
                double ty = Math.Max(0, (h - ft.Height) / 2);
                context.DrawText(ft, new Point(x + 4, ty));
            }
        }

        // Section dividers.
        var divider = new Pen(new SolidColorBrush(palette.Divider), 1);
        foreach (var b in _blocks)
        {
            double x = b.Start * w;
            if (x > 0.5) context.DrawLine(divider, new Point(x, 0), new Point(x, h));
        }

        // Playhead.
        double px = Math.Clamp(PlayPosition, 0, 1) * w;
        context.DrawLine(new Pen(new SolidColorBrush(palette.Playhead), 2), new Point(px, 0), new Point(px, h));
    }

    private static string LabelFor(SegmentKind k) => k switch
    {
        SegmentKind.Intro => "INTRO",   SegmentKind.BuildUp => "BUILD",
        SegmentKind.Drop => "DROP",     SegmentKind.Breakdown => "BREAK",
        SegmentKind.Verse => "VERSE",   SegmentKind.Chorus => "CHORUS",
        SegmentKind.Bridge => "BRIDGE", SegmentKind.Outro => "OUTRO",
        _ => "",
    };
}
