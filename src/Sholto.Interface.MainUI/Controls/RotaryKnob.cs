using System.Globalization;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Data;
using Avalonia.Input;
using Avalonia.Media;
using Avalonia.Media.Immutable;
using Sholto.Interface.MainUI.Controls.Knob;
using Sholto.Interface.MainUI.Theming;

namespace Sholto.Interface.MainUI.Controls;

/// <summary>A rotary knob ("Orbit" look): a flat cap with a pointer, a 270° track ring round it that lights
/// up in the theme's knob arc colour from the stop to the value (with a soft wider glow under it), and ticks (lit once the arc reaches them) with
/// their labels outside. Drawn at 96×96 design units, scaled to fit. Where values sit on the sweep and what
/// they snap to come from the bound <see cref="Scale"/>; the colours from <see cref="Palette"/>. Draws nothing
/// until both arrive.
///
/// Mouse: drag up/down (200 px = the whole sweep), wheel one step per notch, double-click =
/// <see cref="DefaultValue"/>. <see cref="Value"/> binds two-way and always holds a snapped value; the drag
/// itself tracks the unsnapped position, so slow movement still crosses the steps evenly. Keyboard stepping
/// is left to the owner (MainWindow owns the keys while an overlay is open).
///
/// Renders only when the value, scale, palette or size changes; nothing is drawn per frame.</summary>
public sealed class RotaryKnob : Control
{
    /// <summary>Design box the geometry below is measured in.</summary>
    private const double Design = 96;
    private const double CapRadius = 20;
    private const double RingRadius = 28;
    private const double RingWidth = 4;
    /// <summary>The soft light under the lit arc: a wide stroke of the arc colour at low alpha.</summary>
    private const double GlowWidth = 10;
    private const byte GlowAlpha = 0x38;
    private const double TickInner = 32;
    private const double TickOuter = 36;
    private const double TickWidth = 1.5;
    private const double LabelRadius = 43;
    private const double LabelSize = 9;
    private const double PointerInner = 7;
    private const double PointerOuter = 17;
    private const double PointerWidth = 2.5;
    /// <summary>The sweep runs from −135° to +135°, 0° at twelve o'clock, clockwise positive.</summary>
    private const double SweepStartDeg = -135;
    private const double SweepDeg = 270;
    /// <summary>Vertical drag distance that covers the whole sweep.</summary>
    private const double PixelsPerSweep = 200;

    public static readonly StyledProperty<double> ValueProperty =
        AvaloniaProperty.Register<RotaryKnob, double>(nameof(Value), defaultBindingMode: BindingMode.TwoWay);

    public static readonly StyledProperty<double> DefaultValueProperty =
        AvaloniaProperty.Register<RotaryKnob, double>(nameof(DefaultValue));

    public static readonly StyledProperty<IKnobScale?> ScaleProperty =
        AvaloniaProperty.Register<RotaryKnob, IKnobScale?>(nameof(Scale));

    public static readonly StyledProperty<KnobPalette?> PaletteProperty =
        AvaloniaProperty.Register<RotaryKnob, KnobPalette?>(nameof(Palette));

    // Paint cache, rebuilt when the palette or the size changes.
    private KnobPalette? _cachedFor;
    private double _cachedScale = double.NaN;
    private IPen? _trackPen, _glowPen, _arcPen, _tickPen, _litTickPen, _pointerPen, _capPen;
    private IBrush? _capBrush;
    private StreamGeometry? _track;
    private FormattedText[] _labels = [];

    // Drag state.
    private bool _dragging;
    private double _dragStartY;
    private double _dragStartFraction;

    public double Value
    {
        get => GetValue(ValueProperty);
        set => SetValue(ValueProperty, value);
    }

    /// <summary>What a double-click resets to.</summary>
    public double DefaultValue
    {
        get => GetValue(DefaultValueProperty);
        set => SetValue(DefaultValueProperty, value);
    }

    public IKnobScale? Scale
    {
        get => GetValue(ScaleProperty);
        set => SetValue(ScaleProperty, value);
    }

    public KnobPalette? Palette
    {
        get => GetValue(PaletteProperty);
        set => SetValue(PaletteProperty, value);
    }

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        if (change.Property == ScaleProperty) _cachedFor = null;
        if (change.Property == ValueProperty || change.Property == ScaleProperty || change.Property == PaletteProperty)
            InvalidateVisual();
    }

    protected override Size MeasureOverride(Size availableSize)
    {
        double side = Math.Min(
            double.IsInfinity(availableSize.Width) ? Design : availableSize.Width,
            double.IsInfinity(availableSize.Height) ? Design : availableSize.Height);
        return new Size(side, side);
    }

    // ---- Interaction ---------------------------------------------------------------------------

    protected override void OnPointerPressed(PointerPressedEventArgs e)
    {
        base.OnPointerPressed(e);
        if (Scale is not { } scale || !e.GetCurrentPoint(this).Properties.IsLeftButtonPressed) return;
        e.Handled = true;
        if (e.ClickCount >= 2)
        {
            _dragging = false;
            Value = scale.Snap(DefaultValue);
            return;
        }
        _dragging = true;
        _dragStartY = e.GetPosition(this).Y;
        _dragStartFraction = scale.ToFraction(Value);
        e.Pointer.Capture(this);
    }

    protected override void OnPointerMoved(PointerEventArgs e)
    {
        base.OnPointerMoved(e);
        if (!_dragging || Scale is not { } scale) return;
        Value = scale.SnapAt(_dragStartFraction + (_dragStartY - e.GetPosition(this).Y) / PixelsPerSweep);
        e.Handled = true;
    }

    protected override void OnPointerReleased(PointerReleasedEventArgs e)
    {
        base.OnPointerReleased(e);
        if (!_dragging) return;
        _dragging = false;
        e.Pointer.Capture(null);
        e.Handled = true;
    }

    protected override void OnPointerCaptureLost(PointerCaptureLostEventArgs e)
    {
        base.OnPointerCaptureLost(e);
        _dragging = false;
    }

    protected override void OnPointerWheelChanged(PointerWheelEventArgs e)
    {
        base.OnPointerWheelChanged(e);
        if (Scale is not { } scale || e.Delta.Y == 0) return;
        Value = scale.Nudge(Value, Math.Sign(e.Delta.Y));
        e.Handled = true;
    }

    /// <summary>Angle in degrees (0 = twelve o'clock, clockwise) of <paramref name="value"/> on the sweep.</summary>
    private double AngleOf(IKnobScale scale, double value) => SweepStartDeg + SweepDeg * scale.ToFraction(value);

    // ---- Drawing -------------------------------------------------------------------------------

    public override void Render(DrawingContext context)
    {
        if (Palette is not { } palette || Scale is not { } scale) return;
        double side = Math.Min(Bounds.Width, Bounds.Height);
        if (side <= 0) return;
        double k = side / Design;
        var centre = new Point(Bounds.Width / 2, Bounds.Height / 2);
        EnsurePaints(palette, scale, k, centre);

        // Track ring, then the lit arc from the stop to the value.
        context.DrawGeometry(null, _trackPen, _track!);
        double valueDeg = AngleOf(scale, Value);
        if (valueDeg > SweepStartDeg + 0.01)
        {
            var lit = Arc(centre, RingRadius * k, SweepStartDeg, valueDeg);
            context.DrawGeometry(null, _glowPen, lit);
            context.DrawGeometry(null, _arcPen, lit);
        }

        // Ticks (lit once the arc reaches them) and their labels.
        var ticks = scale.Ticks;
        for (int i = 0; i < ticks.Count; i++)
        {
            double deg = AngleOf(scale, ticks[i]);
            // Lit once the arc has reached it; at the bottom stop there is no arc, so nothing is lit.
            var pen = Value > scale.Minimum && Value >= ticks[i] - 1e-9 ? _litTickPen : _tickPen;
            context.DrawLine(pen!, At(centre, TickInner * k, deg), At(centre, TickOuter * k, deg));
            var label = _labels[i];
            var at = At(centre, LabelRadius * k, deg);
            context.DrawText(label, new Point(at.X - label.Width / 2, at.Y - label.Height / 2));
        }

        // Cap and pointer.
        context.DrawEllipse(_capBrush, _capPen, centre, CapRadius * k, CapRadius * k);
        context.DrawLine(_pointerPen!, At(centre, PointerInner * k, valueDeg), At(centre, PointerOuter * k, valueDeg));
    }

    private void EnsurePaints(KnobPalette palette, IKnobScale scale, double k, Point centre)
    {
        if (ReferenceEquals(_cachedFor, palette) && _cachedScale == k && _track is not null) return;
        _cachedFor = palette;
        _cachedScale = k;
        _trackPen = new ImmutablePen(palette.Track.ToUInt32(), RingWidth * k, lineCap: PenLineCap.Round);
        _arcPen = new ImmutablePen(palette.Arc.ToUInt32(), RingWidth * k, lineCap: PenLineCap.Round);
        var glow = new Color(GlowAlpha, palette.Arc.R, palette.Arc.G, palette.Arc.B);
        _glowPen = new ImmutablePen(glow.ToUInt32(), GlowWidth * k, lineCap: PenLineCap.Round);
        _tickPen = new ImmutablePen(palette.Tick.ToUInt32(), TickWidth * k);
        _litTickPen = new ImmutablePen(palette.Arc.ToUInt32(), TickWidth * k);
        _pointerPen = new ImmutablePen(palette.Pointer.ToUInt32(), PointerWidth * k, lineCap: PenLineCap.Round);
        _capPen = new ImmutablePen(palette.CapEdge.ToUInt32(), 1);
        _capBrush = new ImmutableSolidColorBrush(palette.Cap);
        _track = Arc(centre, RingRadius * k, SweepStartDeg, SweepStartDeg + SweepDeg);
        var labelBrush = new ImmutableSolidColorBrush(palette.Tick);
        _labels = scale.Ticks
            .Select(t => new FormattedText(t.ToString("0.##", CultureInfo.InvariantCulture), CultureInfo.InvariantCulture,
                FlowDirection.LeftToRight, Typeface.Default, LabelSize * k, labelBrush))
            .ToArray();
    }

    protected override void OnSizeChanged(SizeChangedEventArgs e)
    {
        base.OnSizeChanged(e);
        _track = null;   // the centre moved: rebuild the track geometry on the next render
    }

    private Point At(Point centre, double radius, double deg)
    {
        double rad = deg * Math.PI / 180;
        return new Point(centre.X + radius * Math.Sin(rad), centre.Y - radius * Math.Cos(rad));
    }

    private StreamGeometry Arc(Point centre, double radius, double fromDeg, double toDeg)
    {
        var geometry = new StreamGeometry();
        using var ctx = geometry.Open();
        ctx.BeginFigure(At(centre, radius, fromDeg), isFilled: false);
        ctx.ArcTo(At(centre, radius, toDeg), new Size(radius, radius), 0, toDeg - fromDeg > 180, SweepDirection.Clockwise);
        ctx.EndFigure(false);
        return geometry;
    }
}
