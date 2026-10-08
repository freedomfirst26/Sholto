using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;
using Sholto.Interface.MainUI.ViewModels.Glance;

namespace Sholto.Interface.MainUI.Controls;

/// <summary>
/// The platter in a Glance LOAD TO slot, drawn in a 30-unit box and scaled to the control's size (52 px in a slot). A loaded deck is a dark disc with a marker that turns (one
/// turn per bar, <see cref="IDeckSlotViewModel.SpinTurns"/>), the deck number on its label, and a ring around
/// it of the time left: StatusOk while playing (LowBrush under 45 s), TextMuted while paused, on a Border track. The deck number is
/// not drawn here: the slot lays a TextBlock over the label, because drawing text lays it out again on every
/// frame.
/// <para>In <see cref="Ghost"/> mode the control is the empty slot's 64 px record instead, drawn only while the
/// deck is empty: a radial-gradient disc (SurfaceRaised into BgDeep) with faint grooves, a highlight arc and a
/// label circle, ringed and labelled in Accent when the slot is the load target. The slot crops it at its
/// left edge and lays the deck number over the visible part.</para>
///
/// Plain <see cref="DrawingContext"/> primitives. Pens and the ring's geometry are built once and rebuilt
/// only when a brush or a whole degree of ring changes, so a frame that only turns
/// the platter allocates nothing. A control created from XAML draws nothing until its brushes and slot arrive.
/// </summary>
public sealed class DeckSlotPlatter : Control
{
    private const double Size = 30;
    private const double Centre = Size / 2;
    private const double RingRadius = 13.5;
    private const double RingWidth = 2.9; // 5 px at the 52 px the slot draws it
    private const double DiscRadius = 11;
    private const double GrooveRadius = 8.5;
    private const double LabelRadius = 6;

    // The ghost record is drawn in a 30-unit box like the platter and scaled up to GhostSize.
    private const double GhostSize = 64;
    private const double GhostScale = GhostSize / Size;
    private const double GhostDiscRadius = 14;
    private const double GhostLabelRadius = 5.6;
    private const double GhostGrooveFirst = 12.6;
    private const double GhostGrooveStep = 1.6;
    private const int GhostGrooves = 4;
    private const double GhostArcOpacity = 0.45;

    public static readonly StyledProperty<IDeckSlotViewModel?> SlotProperty =
        AvaloniaProperty.Register<DeckSlotPlatter, IDeckSlotViewModel?>(nameof(Slot));

    /// <summary>The ring's track (the theme's Border).</summary>
    public static readonly StyledProperty<IBrush?> TrackBrushProperty =
        AvaloniaProperty.Register<DeckSlotPlatter, IBrush?>(nameof(TrackBrush));

    /// <summary>The ring while playing (StatusOk).</summary>
    public static readonly StyledProperty<IBrush?> PlayingBrushProperty =
        AvaloniaProperty.Register<DeckSlotPlatter, IBrush?>(nameof(PlayingBrush));

    /// <summary>The ring while paused, and the ghost record's highlight arc (TextMuted).</summary>
    public static readonly StyledProperty<IBrush?> MutedBrushProperty =
        AvaloniaProperty.Register<DeckSlotPlatter, IBrush?>(nameof(MutedBrush));

    /// <summary>The ring while the deck is playing with under 45 s left (StatusError).</summary>
    public static readonly StyledProperty<IBrush?> LowBrushProperty =
        AvaloniaProperty.Register<DeckSlotPlatter, IBrush?>(nameof(LowBrush));

    /// <summary>An empty target's record rim and label (Accent).</summary>
    public static readonly StyledProperty<IBrush?> AccentBrushProperty =
        AvaloniaProperty.Register<DeckSlotPlatter, IBrush?>(nameof(AccentBrush));

    /// <summary>The disc (BgDeep).</summary>
    public static readonly StyledProperty<IBrush?> DiscBrushProperty =
        AvaloniaProperty.Register<DeckSlotPlatter, IBrush?>(nameof(DiscBrush));

    /// <summary>The label under the number (Surface).</summary>
    public static readonly StyledProperty<IBrush?> LabelBrushProperty =
        AvaloniaProperty.Register<DeckSlotPlatter, IBrush?>(nameof(LabelBrush));

    /// <summary>The marker on the disc and a loaded deck's number (TextBright).</summary>
    public static readonly StyledProperty<IBrush?> MarkerBrushProperty =
        AvaloniaProperty.Register<DeckSlotPlatter, IBrush?>(nameof(MarkerBrush));

    /// <summary>Draw the empty slot's ghost record instead of the loaded platter.</summary>
    public static readonly StyledProperty<bool> GhostProperty =
        AvaloniaProperty.Register<DeckSlotPlatter, bool>(nameof(Ghost));

    /// <summary>The ghost record's disc highlight and label (SurfaceRaised).</summary>
    public static readonly StyledProperty<IBrush?> RaisedBrushProperty =
        AvaloniaProperty.Register<DeckSlotPlatter, IBrush?>(nameof(RaisedBrush));

    private readonly Action _onSlotChanged;
    private readonly StreamGeometry _arc = new();

    private IPen? _trackPen;
    private IPen? _playingPen;
    private IPen? _pausedPen;
    private IPen? _lowPen;
    private IPen? _groovePen;
    private IPen? _ghostRimPen;
    private IPen? _ghostRimAccentPen;
    private IPen? _ghostGroovePen;
    private IPen? _ghostArcPen;
    private readonly IBrush?[] _penBrushes = new IBrush?[9];
    private readonly StreamGeometry _ghostArc = new();
    private IBrush? _recordBrush;
    private IBrush? _recordFromDisc;
    private IBrush? _recordFromRaised;
    private int _arcStep = -1;

    static DeckSlotPlatter()
    {
        AffectsRender<DeckSlotPlatter>(
            TrackBrushProperty, PlayingBrushProperty, MutedBrushProperty, LowBrushProperty, AccentBrushProperty,
            DiscBrushProperty, LabelBrushProperty, MarkerBrushProperty, RaisedBrushProperty, GhostProperty);
        AffectsMeasure<DeckSlotPlatter>(GhostProperty);
        SlotProperty.Changed.AddClassHandler<DeckSlotPlatter>((c, e) => c.Rewire(e));
    }

    public DeckSlotPlatter()
    {
        IsHitTestVisible = false;
        _onSlotChanged = InvalidateVisual;
        BuildGhostArc();
    }

    public IDeckSlotViewModel? Slot { get => GetValue(SlotProperty); set => SetValue(SlotProperty, value); }
    public IBrush? TrackBrush { get => GetValue(TrackBrushProperty); set => SetValue(TrackBrushProperty, value); }
    public IBrush? PlayingBrush { get => GetValue(PlayingBrushProperty); set => SetValue(PlayingBrushProperty, value); }
    public IBrush? MutedBrush { get => GetValue(MutedBrushProperty); set => SetValue(MutedBrushProperty, value); }
    public IBrush? LowBrush { get => GetValue(LowBrushProperty); set => SetValue(LowBrushProperty, value); }
    public IBrush? AccentBrush { get => GetValue(AccentBrushProperty); set => SetValue(AccentBrushProperty, value); }
    public IBrush? DiscBrush { get => GetValue(DiscBrushProperty); set => SetValue(DiscBrushProperty, value); }
    public IBrush? LabelBrush { get => GetValue(LabelBrushProperty); set => SetValue(LabelBrushProperty, value); }
    public IBrush? RaisedBrush { get => GetValue(RaisedBrushProperty); set => SetValue(RaisedBrushProperty, value); }
    public bool Ghost { get => GetValue(GhostProperty); set => SetValue(GhostProperty, value); }
    public IBrush? MarkerBrush { get => GetValue(MarkerBrushProperty); set => SetValue(MarkerBrushProperty, value); }

    /// <summary>How many times a pen, or the ring's geometry has been built. A frame that only
    /// turns the platter leaves it where it was.</summary>
    internal int PaintBuilds { get; private set; }

    /// <summary>The brush the ring's arc was last drawn with; what a headless test reads in place of a pixel.</summary>
    internal IBrush? RingBrush { get; private set; }

    protected override Size MeasureOverride(Size availableSize)
    {
        if (Ghost) return new Size(GhostSize, GhostSize);
        return new Size(double.IsNaN(Width) ? Size : Width, double.IsNaN(Height) ? Size : Height);
    }

    private void Rewire(AvaloniaPropertyChangedEventArgs e)
    {
        if (e.OldValue is IDeckSlotViewModel old) old.Changed -= _onSlotChanged;
        if (e.NewValue is IDeckSlotViewModel now) now.Changed += _onSlotChanged;
        InvalidateVisual();
    }

    public override void Render(DrawingContext context)
    {
        if (Slot is not { } slot) return;
        if (Ghost)
        {
            if (!slot.IsLoaded) DrawGhost(context, slot);
            return;
        }
        if (TrackBrush is null || PlayingBrush is null || MutedBrush is null || AccentBrush is null
            || DiscBrush is null || LabelBrush is null || MarkerBrush is null) return;
        EnsurePens();

        var centre = new Point(Centre, Centre);
        if (!slot.IsLoaded) return;

        // The drawing is 30 units wide; it scales to the control's own size (52 px in a slot).
        var scale = Bounds.Width > 0 ? Bounds.Width / Size : 1;
        using var scaled = context.PushTransform(Matrix.CreateScale(scale, scale));

        context.DrawEllipse(null, _trackPen, centre, RingRadius, RingRadius);
        DrawRing(context, slot);

        context.DrawEllipse(DiscBrush, null, centre, DiscRadius, DiscRadius);
        context.DrawEllipse(null, _groovePen, centre, GrooveRadius, GrooveRadius);
        // The marker is a short bar at 12 o'clock that turns about the centre.
        using (context.PushTransform(Matrix.CreateTranslation(-Centre, -Centre)
                   * Matrix.CreateRotation(slot.SpinTurns * 2 * Math.PI)
                   * Matrix.CreateTranslation(Centre, Centre)))
            context.DrawRectangle(MarkerBrush, null, new RoundedRect(new Rect(14.4, 4.4, 1.2, 3.4), 0.6));
        context.DrawEllipse(LabelBrush, null, centre, LabelRadius, LabelRadius);
    }

    private void DrawGhost(DrawingContext context, IDeckSlotViewModel slot)
    {
        if (TrackBrush is null || MutedBrush is null || AccentBrush is null || DiscBrush is null || RaisedBrush is null) return;
        EnsurePens();
        EnsureRecordBrush();
        var centre = new Point(Centre, Centre);
        using (context.PushTransform(Matrix.CreateScale(GhostScale, GhostScale)))
        {
            context.DrawEllipse(_recordBrush, slot.IsTarget ? _ghostRimAccentPen : _ghostRimPen, centre, GhostDiscRadius, GhostDiscRadius);
            for (var i = 0; i < GhostGrooves; i++)
            {
                var r = GhostGrooveFirst - i * GhostGrooveStep;
                context.DrawEllipse(null, _ghostGroovePen, centre, r, r);
            }
            using (context.PushOpacity(GhostArcOpacity))
                context.DrawGeometry(null, _ghostArcPen, _ghostArc);
            context.DrawEllipse(slot.IsTarget ? AccentBrush : RaisedBrush, null, centre, GhostLabelRadius, GhostLabelRadius);
        }
    }

    /// <summary>The disc's radial gradient, rebuilt only when the theme hands over a new BgDeep or SurfaceRaised.</summary>
    private void EnsureRecordBrush()
    {
        if (ReferenceEquals(_recordFromDisc, DiscBrush) && ReferenceEquals(_recordFromRaised, RaisedBrush) && _recordBrush is not null) return;
        _recordFromDisc = DiscBrush;
        _recordFromRaised = RaisedBrush;
        PaintBuilds++;
        _recordBrush = DiscBrush is ISolidColorBrush disc && RaisedBrush is ISolidColorBrush raised
            ? new RadialGradientBrush
            {
                Center = new RelativePoint(0.5, 0.4, RelativeUnit.Relative),
                GradientOrigin = new RelativePoint(0.5, 0.4, RelativeUnit.Relative),
                Radius = 0.6,
                GradientStops = [new GradientStop(raised.Color, 0), new GradientStop(disc.Color, 1)],
            }
            : DiscBrush;
    }

    /// <summary>The highlight arc on the record's upper left, from (7,7) to (15,3.7).</summary>
    private void BuildGhostArc()
    {
        using var c = _ghostArc.Open();
        c.BeginFigure(new Point(7, 7), false);
        c.ArcTo(new Point(15, 3.7), new Size(11.3, 11.3), 0, false, SweepDirection.Clockwise);
        c.EndFigure(false);
    }

    private void DrawRing(DrawingContext context, IDeckSlotViewModel slot)
    {
        var remaining = slot.RemainingFraction;
        if (remaining <= 0) return;
        var pen = slot.IsLow && _lowPen is not null ? _lowPen : slot.IsPlaying ? _playingPen : _pausedPen;
        RingBrush = pen?.Brush;
        if (remaining >= 1)
        {
            context.DrawEllipse(null, pen, new Point(Centre, Centre), RingRadius, RingRadius);
            return;
        }
        var step = (int)Math.Round(remaining * DeckSlotViewModel.Steps);
        if (step != _arcStep) BuildArc(step);
        context.DrawGeometry(null, pen, _arc);
    }

    /// <summary>The ring from 12 o'clock clockwise through <paramref name="step"/> degrees, into the one geometry.</summary>
    private void BuildArc(int step)
    {
        _arcStep = step;
        PaintBuilds++;
        var angle = step * 2 * Math.PI / DeckSlotViewModel.Steps;
        var end = new Point(Centre + RingRadius * Math.Sin(angle), Centre - RingRadius * Math.Cos(angle));
        using var g = _arc.Open();
        g.BeginFigure(new Point(Centre, Centre - RingRadius), false);
        g.ArcTo(end, new Size(RingRadius, RingRadius), 0, step > DeckSlotViewModel.Steps / 2, SweepDirection.Clockwise);
        g.EndFigure(false);
    }

    /// <summary>Rebuild the pens whose brush changed (a theme switch hands over new brushes).</summary>
    private void EnsurePens()
    {
        Refresh(0, TrackBrush, ref _trackPen, b => new Pen(b, RingWidth));
        Refresh(1, PlayingBrush, ref _playingPen, b => new Pen(b, RingWidth, lineCap: PenLineCap.Round));
        Refresh(2, MutedBrush, ref _pausedPen, b => new Pen(b, RingWidth, lineCap: PenLineCap.Round));
        Refresh(3, TrackBrush, ref _groovePen, b => new Pen(b, 0.6));
        Refresh(4, TrackBrush, ref _ghostRimPen, b => new Pen(b, 0.8));
        Refresh(5, AccentBrush, ref _ghostRimAccentPen, b => new Pen(b, 1.2));
        Refresh(6, TrackBrush, ref _ghostGroovePen, b => new Pen(b, 0.5));
        Refresh(7, MutedBrush, ref _ghostArcPen, b => new Pen(b, 0.7, lineCap: PenLineCap.Round));
        Refresh(8, LowBrush, ref _lowPen, b => new Pen(b, RingWidth, lineCap: PenLineCap.Round));
    }

    private void Refresh(int slot, IBrush? brush, ref IPen? pen, Func<IBrush, IPen> build)
    {
        if (ReferenceEquals(_penBrushes[slot], brush) && (brush is null || pen is not null)) return;
        _penBrushes[slot] = brush;
        pen = brush is null ? null : build(brush);
        PaintBuilds++;
    }
}
