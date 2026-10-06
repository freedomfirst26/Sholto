using Avalonia;
using Avalonia.Animation;
using Avalonia.Animation.Easings;
using Avalonia.Controls;
using Avalonia.Media;
using Avalonia.Media.Transformation;

namespace Sholto.Interface.MainUI.Controls.CollapseToIcon;

/// <summary>Hosts any overlay or panel that can be dismissed into an icon. While its
/// <see cref="Sequence"/> is shown it is visible; on collapse it shrinks into the
/// <see cref="Target"/>'s centre and fades, and on open it grows back out of it. Under reduced motion it
/// only fades. The sequence decides when; Avalonia's transitions do the pixels, starting from wherever the
/// last one stopped, so opening mid-collapse reverses smoothly.</summary>
public sealed class IconDockedContainer : ContentControl
{
    private const double CollapsedScale = 0.02;

    public static readonly StyledProperty<ICollapseToIconSequence?> SequenceProperty =
        AvaloniaProperty.Register<IconDockedContainer, ICollapseToIconSequence?>(nameof(Sequence));

    public static readonly StyledProperty<ICollapseTarget?> TargetProperty =
        AvaloniaProperty.Register<IconDockedContainer, ICollapseTarget?>(nameof(Target));

    private readonly SplineEasing _easeIn = new(0.55, 0, 0.8, 0.25);
    private readonly SplineEasing _easeOut = new(0.2, 0.6, 0.35, 1);

    private readonly TransformOperations _collapsed;
    private readonly TransformOperations _identity;
    private Transitions? _collapseTransitions;
    private Transitions? _growTransitions;
    private Transitions? _fadeOutTransitions;
    private Transitions? _fadeInTransitions;
    private ICollapseToIconSequence? _subscribed;

    public IconDockedContainer()
    {
        _collapsed = Scale(CollapsedScale);
        _identity = Scale(1);
    }

    public ICollapseToIconSequence? Sequence
    {
        get => GetValue(SequenceProperty);
        set => SetValue(SequenceProperty, value);
    }

    public ICollapseTarget? Target
    {
        get => GetValue(TargetProperty);
        set => SetValue(TargetProperty, value);
    }

    protected override Type StyleKeyOverride => typeof(ContentControl);

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        if (change.Property != SequenceProperty) return;
        if (_subscribed is not null) _subscribed.Changed -= Apply;
        _subscribed = Sequence;
        _collapseTransitions = _growTransitions = _fadeOutTransitions = _fadeInTransitions = null;
        if (_subscribed is null) return;
        _subscribed.Changed += Apply;
        Apply();
    }

    private TransformOperations Scale(double factor)
    {
        var builder = TransformOperations.CreateBuilder(1);
        builder.AppendScale(factor, factor);
        return builder.Build();
    }

    private void Apply()
    {
        var sequence = Sequence;
        if (sequence is null) return;
        var timings = sequence.Timings;

        IsVisible = sequence.IsShown;
        IsHitTestVisible = sequence.State == CollapseToIconState.Open;
        if (sequence.State is CollapseToIconState.Open or CollapseToIconState.Collapsing) PointAtTarget();

        switch (sequence.State)
        {
            case CollapseToIconState.Open:
                Transitions = sequence.Reduced ? FadeIn(timings) : Grow(timings);
                Opacity = 1;
                if (!sequence.Reduced) RenderTransform = _identity;
                break;
            case CollapseToIconState.Collapsing:
                Transitions = sequence.Reduced ? FadeOut(timings) : Collapse(timings);
                Opacity = 0;
                if (!sequence.Reduced) RenderTransform = _collapsed;
                break;
            default:
                // Hidden in the icon: rest at the collapsed values so the next open grows from them.
                Transitions = null;
                Opacity = 0;
                if (!sequence.Reduced) RenderTransform = _collapsed;
                break;
        }
    }

    private void PointAtTarget()
    {
        if (Sequence is { Reduced: true }) return;
        if (Target?.CenterIn(this) is { } centre)
            RenderTransformOrigin = new RelativePoint(centre, RelativeUnit.Absolute);
    }

    private Transitions Collapse(CollapseToIconTimings timings) => _collapseTransitions ??=
    [
        new TransformOperationsTransition { Property = RenderTransformProperty, Duration = timings.Collapse, Easing = _easeIn },
        new DoubleTransition { Property = OpacityProperty, Duration = timings.Collapse, Easing = _easeIn },
    ];

    private Transitions Grow(CollapseToIconTimings timings) => _growTransitions ??=
    [
        new TransformOperationsTransition { Property = RenderTransformProperty, Duration = timings.Grow, Easing = _easeOut },
        new DoubleTransition { Property = OpacityProperty, Duration = timings.Grow, Easing = _easeOut },
    ];

    private Transitions FadeOut(CollapseToIconTimings timings) => _fadeOutTransitions ??=
    [
        new DoubleTransition { Property = OpacityProperty, Duration = timings.ReducedFade },
    ];

    private Transitions FadeIn(CollapseToIconTimings timings) => _fadeInTransitions ??=
    [
        new DoubleTransition { Property = OpacityProperty, Duration = timings.ReducedFade },
    ];
}
