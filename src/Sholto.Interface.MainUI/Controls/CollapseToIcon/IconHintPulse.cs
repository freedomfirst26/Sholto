using Avalonia;
using Avalonia.Animation;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Media;
using Avalonia.Styling;
using Avalonia.VisualTree;

namespace Sholto.Interface.MainUI.Controls.CollapseToIcon;

/// <summary>Wraps an icon so a collapsing <see cref="IconDockedContainer"/> has somewhere to go and the
/// icon can call attention to itself afterwards. While the sequence is hinting it sets the
/// <c>:hinting</c> pseudo-class (styles tint the icon with it) and draws accent rings pulsing out from
/// behind it, or a still outline under reduced motion. Hovering the icon ends the hint. A control paints only
/// inside its own bounds, so give it a Padding (and the matching negative Margin, to keep the layout size)
/// wide enough for the rings.</summary>
public sealed class IconHintPulse : ContentControl, ICollapseTarget
{
    private const double RingRadius = 15;
    private const double HaloRadius = 22;
    private const double StartScale = 0.7;
    private const double EndScale = 2.1;
    private const double RingOpacity = 0.85;
    private const double HaloOpacity = 0.9;
    private const double HaloPeak = 0.3;
    private const double HaloAlpha = 0.55;
    private const double OutlineGap = 3;

    public static readonly StyledProperty<ICollapseToIconSequence?> SequenceProperty =
        AvaloniaProperty.Register<IconHintPulse, ICollapseToIconSequence?>(nameof(Sequence));

    public static readonly StyledProperty<IBrush?> RingBrushProperty =
        AvaloniaProperty.Register<IconHintPulse, IBrush?>(nameof(RingBrush));

    /// <summary>How far through the current pulse the rings are, 0 to 1. Animated; read by <see cref="Render"/>.</summary>
    public static readonly StyledProperty<double> PulsePhaseProperty =
        AvaloniaProperty.Register<IconHintPulse, double>(nameof(PulsePhase));

    private ICollapseToIconSequence? _subscribed;
    private CancellationTokenSource? _pulse;
    private IBrush? _penBrush;
    private IPen? _ringPen;
    private IPen? _outlinePen;
    private IBrush? _haloBrush;

    static IconHintPulse()
    {
        AffectsRender<IconHintPulse>(PulsePhaseProperty, RingBrushProperty);
    }

    public ICollapseToIconSequence? Sequence
    {
        get => GetValue(SequenceProperty);
        set => SetValue(SequenceProperty, value);
    }

    public IBrush? RingBrush
    {
        get => GetValue(RingBrushProperty);
        set => SetValue(RingBrushProperty, value);
    }

    public double PulsePhase
    {
        get => GetValue(PulsePhaseProperty);
        set => SetValue(PulsePhaseProperty, value);
    }

    protected override Type StyleKeyOverride => typeof(ContentControl);

    public Point? CenterIn(Visual relativeTo) =>
        this.TranslatePoint(new Point(Bounds.Width / 2, Bounds.Height / 2), relativeTo);

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        if (change.Property == SequenceProperty)
        {
            if (_subscribed is not null) _subscribed.Changed -= Apply;
            _subscribed = Sequence;
            if (_subscribed is not null) _subscribed.Changed += Apply;
            Apply();
        }
        else if (change.Property == RingBrushProperty)
        {
            _penBrush = null;
        }
    }

    protected override void OnPointerEntered(PointerEventArgs e)
    {
        base.OnPointerEntered(e);
        Sequence?.TargetEngaged();
    }

    private void Apply()
    {
        var hinting = Sequence?.State == CollapseToIconState.Hinting;
        PseudoClasses.Set(":hinting", hinting);
        StopPulse();
        if (hinting && Sequence is { IsHintStatic: false } sequence) StartPulse(sequence.Timings);
        InvalidateVisual();
    }

    private void StartPulse(CollapseToIconTimings timings)
    {
        _pulse = new CancellationTokenSource();
        var animation = new Animation
        {
            Duration = timings.PulsePeriod,
            IterationCount = new IterationCount((ulong)timings.PulseCount),
            Children =
            {
                new KeyFrame { Cue = new Cue(0), Setters = { new Setter(PulsePhaseProperty, 0.0) } },
                new KeyFrame { Cue = new Cue(1), Setters = { new Setter(PulsePhaseProperty, 1.0) } },
            },
        };
        _ = animation.RunAsync(this, _pulse.Token);
    }

    private void StopPulse()
    {
        if (_pulse is null) return;
        _pulse.Cancel();
        _pulse.Dispose();
        _pulse = null;
        ClearValue(PulsePhaseProperty);
    }

    public override void Render(DrawingContext context)
    {
        base.Render(context);
        var sequence = Sequence;
        if (sequence is null || sequence.State != CollapseToIconState.Hinting) return;
        if (RingBrush is not ISolidColorBrush solid) return;
        EnsurePaints(solid);

        var centre = new Point(Bounds.Width / 2, Bounds.Height / 2);
        if (sequence.IsHintStatic)
        {
            var box = new Rect(Bounds.Size).Inflate(OutlineGap);
            context.DrawRectangle(null, _outlinePen, box, 5, 5);
            return;
        }

        var phase = PulsePhase;
        // Halo: fades in to its peak, then out. Ring: grows with an ease-out and fades.
        var halo = phase < HaloPeak ? phase / HaloPeak : 1 - (phase - HaloPeak) / (1 - HaloPeak);
        using (context.PushOpacity(halo * HaloOpacity))
            context.DrawEllipse(_haloBrush, null, centre, HaloRadius, HaloRadius);
        var eased = 1 - (1 - phase) * (1 - phase) * (1 - phase);
        var radius = RingRadius * (StartScale + (EndScale - StartScale) * eased);
        using (context.PushOpacity(RingOpacity * (1 - phase)))
            context.DrawEllipse(null, _ringPen, centre, radius, radius);
    }

    private void EnsurePaints(ISolidColorBrush solid)
    {
        if (ReferenceEquals(_penBrush, solid)) return;
        _penBrush = solid;
        _ringPen = new Pen(solid, 2);
        _outlinePen = new Pen(solid, 2);
        var colour = solid.Color;
        _haloBrush = new RadialGradientBrush
        {
            GradientStops =
            [
                new GradientStop(Color.FromArgb((byte)(colour.A * HaloAlpha), colour.R, colour.G, colour.B), 0),
                new GradientStop(Color.FromArgb(0, colour.R, colour.G, colour.B), 0.68),
            ],
        };
    }
}
