using Avalonia;
using Sholto.Interface.MainUI.Controls.CollapseToIcon;

namespace Sholto.Interface.MainUI.Tests.CollapseToIcon;

/// <summary>The container as a control: visibility, hit-testing, the collapse origin and reduced motion.</summary>
public class IconDockedContainerTests
{
    private readonly FakeFrameClock _clock = new();

    public IconDockedContainerTests() => AvaloniaTestApp.EnsureStarted();

    private CollapseToIconSequence Sequence(bool reduced = false) => new(
        _clock, new FixedMotionPreference(reduced),
        new CollapseToIconOptions("test", new CollapseToIconTimingsFactory().Standard()), new AlwaysHintPolicy());

    [Fact]
    public void Visibility_follows_whether_the_sequence_is_shown()
    {
        var sequence = Sequence();
        var container = new IconDockedContainer { Sequence = sequence };
        Assert.False(container.IsVisible);

        sequence.Open();
        Assert.True(container.IsVisible);

        sequence.Collapse();
        Assert.True(container.IsVisible);

        _clock.Advance(0.4);
        sequence.OnFrame(_clock.Now);
        Assert.Equal(CollapseToIconState.Hinting, sequence.State);
        Assert.False(container.IsVisible);
    }

    [Fact]
    public void Hit_testing_is_off_while_collapsing()
    {
        var sequence = Sequence();
        var container = new IconDockedContainer { Sequence = sequence };

        sequence.Open();
        Assert.True(container.IsHitTestVisible);

        sequence.Collapse();
        Assert.False(container.IsHitTestVisible);

        sequence.Open();
        Assert.True(container.IsHitTestVisible);
    }

    [Fact]
    public void The_transform_origin_is_the_targets_centre()
    {
        var sequence = Sequence();
        var container = new IconDockedContainer { Sequence = sequence, Target = new FixedCollapseTarget(new Point(1100, 14)) };

        sequence.Open();
        sequence.Collapse();

        Assert.Equal(RelativeUnit.Absolute, container.RenderTransformOrigin.Unit);
        Assert.Equal(new Point(1100, 14), container.RenderTransformOrigin.Point);
    }

    [Fact]
    public void Collapsing_scales_down_and_fades_out_and_opening_restores_both()
    {
        var sequence = Sequence();
        var container = new IconDockedContainer { Sequence = sequence };

        sequence.Open();
        var openTransform = container.RenderTransform;
        Assert.Equal(1, container.Opacity);

        sequence.Collapse();
        Assert.NotNull(container.RenderTransform);
        Assert.NotSame(openTransform, container.RenderTransform);
        Assert.Equal(0, container.Opacity);

        sequence.Open();
        Assert.Same(openTransform, container.RenderTransform);
        Assert.Equal(1, container.Opacity);
    }

    [Fact]
    public void Reduced_motion_never_sets_a_transform()
    {
        var sequence = Sequence(reduced: true);
        var container = new IconDockedContainer { Sequence = sequence };

        sequence.Open();
        sequence.Collapse();
        _clock.Advance(1);
        sequence.OnFrame(_clock.Now);
        sequence.Open();

        Assert.Null(container.RenderTransform);
    }

    [Fact]
    public void Reduced_motion_still_fades()
    {
        var sequence = Sequence(reduced: true);
        var container = new IconDockedContainer { Sequence = sequence };

        sequence.Open();
        Assert.Equal(1, container.Opacity);

        sequence.Collapse();
        Assert.Equal(0, container.Opacity);
    }
}
