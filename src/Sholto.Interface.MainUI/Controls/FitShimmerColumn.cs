using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;

namespace Sholto.Interface.MainUI.Controls;

/// <summary>
/// The slow indicator of the Glance table: while a ranking is being redone, the FIT column's bars go neutral
/// and a band of the theme's primary colour walks down them, each bar a moment after the one above. It is laid
/// over the column, draws the bar pattern itself (a 6 x 30 bar every 42 px, shifted by the list's scroll offset)
/// and is hidden while nothing is being redone.
///
/// Plain <see cref="DrawingContext"/> primitives and nothing allocated per frame: the band brush is built once and
/// only its stop colours change with the theme. <see cref="Phase"/> (0 to 1, one loop) is driven by an Avalonia
/// animation from the view's style; with <see cref="Animated"/> off (reduced motion) the bars are neutral and
/// nothing moves. A control created from XAML draws nothing until its brushes arrive.
/// </summary>
public sealed class FitShimmerColumn : Control
{
    /// <summary>The loop position, 0 to 1. Animated by the view's style.</summary>
    public static readonly StyledProperty<double> PhaseProperty =
        AvaloniaProperty.Register<FitShimmerColumn, double>(nameof(Phase));

    /// <summary>The list's vertical scroll offset in px: the bar pattern follows the rows under it.</summary>
    public static readonly StyledProperty<double> ScrollOffsetProperty =
        AvaloniaProperty.Register<FitShimmerColumn, double>(nameof(ScrollOffset));

    /// <summary>How many rows the list holds; bars are drawn only for those.</summary>
    public static readonly StyledProperty<int> RowCountProperty =
        AvaloniaProperty.Register<FitShimmerColumn, int>(nameof(RowCount));

    /// <summary>The band's colour: the theme's primary.</summary>
    public static readonly StyledProperty<IBrush?> LightProperty =
        AvaloniaProperty.Register<FitShimmerColumn, IBrush?>(nameof(Light));

    /// <summary>The neutral bar colour: the theme's muted text colour, drawn at 35 %.</summary>
    public static readonly StyledProperty<IBrush?> NeutralProperty =
        AvaloniaProperty.Register<FitShimmerColumn, IBrush?>(nameof(Neutral));

    /// <summary>Whether the band moves. False under reduced motion: neutral bars only.</summary>
    public static readonly StyledProperty<bool> AnimatedProperty =
        AvaloniaProperty.Register<FitShimmerColumn, bool>(nameof(Animated), true);

    private const double RowPitch = 42;
    private const double BarWidth = 6;
    private const double BarHeight = 30;
    private const double NeutralOpacity = 0.35;

    /// <summary>One loop is a second; each bar lags the one above by this much of it.</summary>
    private const double StaggerPerRow = 0.055;

    /// <summary>The band crosses a bar in this share of the loop, then waits for the next.</summary>
    private const double TravelShare = 0.6;

    private readonly LinearGradientBrush _band;

    static FitShimmerColumn()
    {
        AffectsRender<FitShimmerColumn>(PhaseProperty, ScrollOffsetProperty, RowCountProperty, NeutralProperty, AnimatedProperty);
        LightProperty.Changed.AddClassHandler<FitShimmerColumn>((c, _) => c.Retone());
    }

    public FitShimmerColumn()
    {
        IsHitTestVisible = false;
        _band = new LinearGradientBrush
        {
            StartPoint = new RelativePoint(0, 0, RelativeUnit.Relative),
            EndPoint = new RelativePoint(0, 1, RelativeUnit.Relative),
            GradientStops =
            {
                new GradientStop(default, 0.0),
                new GradientStop(default, 0.5),
                new GradientStop(default, 1.0),
            },
        };
    }

    public double Phase { get => GetValue(PhaseProperty); set => SetValue(PhaseProperty, value); }
    public double ScrollOffset { get => GetValue(ScrollOffsetProperty); set => SetValue(ScrollOffsetProperty, value); }
    public int RowCount { get => GetValue(RowCountProperty); set => SetValue(RowCountProperty, value); }
    public IBrush? Light { get => GetValue(LightProperty); set => SetValue(LightProperty, value); }
    public IBrush? Neutral { get => GetValue(NeutralProperty); set => SetValue(NeutralProperty, value); }
    public bool Animated { get => GetValue(AnimatedProperty); set => SetValue(AnimatedProperty, value); }

    // The band fades from nothing to the primary colour and back, so its edges never show as a line.
    private void Retone()
    {
        if (Light is not ISolidColorBrush solid) return;
        var c = solid.Color;
        _band.GradientStops[0].Color = Color.FromArgb(0x00, c.R, c.G, c.B);
        _band.GradientStops[1].Color = c;
        _band.GradientStops[2].Color = Color.FromArgb(0x00, c.R, c.G, c.B);
        InvalidateVisual();
    }

    public override void Render(DrawingContext context)
    {
        if (Neutral is not { } neutral || RowCount <= 0) return;

        var offset = Math.Max(0, ScrollOffset);
        var first = (int)(offset / RowPitch);
        var last = Math.Min(RowCount - 1, (int)((offset + Bounds.Height) / RowPitch));
        var inset = (RowPitch - BarHeight) / 2;
        var showBand = Animated && Light is not null;
        var phase = Phase;

        for (var row = first; row <= last; row++)
        {
            var top = row * RowPitch - offset + inset;
            var bar = new Rect(0, top, BarWidth, BarHeight);
            var shape = new RoundedRect(bar, BarWidth / 2);

            using (context.PushOpacity(NeutralOpacity))
                context.DrawRectangle(neutral, null, shape);

            if (!showBand) continue;

            // Where this bar is in its own loop: the band is off the bar for the rest of it.
            var local = phase - (row - first) * StaggerPerRow;
            local -= Math.Floor(local);
            if (local >= TravelShare) continue;

            // From one bar-height above the bar to one below it.
            var shift = (local / TravelShare * 2 - 1) * BarHeight;
            using (context.PushClip(shape))
            using (context.PushTransform(Matrix.CreateTranslation(0, shift)))
                context.DrawRectangle(_band, null, bar);
        }
    }
}
