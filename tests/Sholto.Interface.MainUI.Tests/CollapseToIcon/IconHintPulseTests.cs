using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Media;
using Sholto.Interface.MainUI.Controls.CollapseToIcon;

namespace Sholto.Interface.MainUI.Tests.CollapseToIcon;

/// <summary>The icon wrapper as a control: the :hinting pseudo-class, hover ending the hint, and its centre.</summary>
public class IconHintPulseTests
{
    private readonly FakeFrameClock _clock = new();

    public IconHintPulseTests() => AvaloniaTestApp.EnsureStarted();

    private CollapseToIconSequence Sequence(bool reduced = false) => new(
        _clock, new FixedMotionPreference(reduced),
        new CollapseToIconOptions("test", new CollapseToIconTimingsFactory().Standard()), new AlwaysHintPolicy());

    [Fact]
    public void Hinting_is_set_exactly_while_the_sequence_hints()
    {
        var sequence = Sequence();
        var pulse = new IconHintPulse { Sequence = sequence };
        Assert.False(pulse.Classes.Contains(":hinting"));

        sequence.Open();
        Assert.False(pulse.Classes.Contains(":hinting"));

        sequence.Collapse();
        Assert.False(pulse.Classes.Contains(":hinting"));

        _clock.Advance(0.35);
        sequence.OnFrame(_clock.Now);
        Assert.True(pulse.Classes.Contains(":hinting"));

        sequence.TargetEngaged();
        Assert.False(pulse.Classes.Contains(":hinting"));
    }

    [Fact]
    public void Hinting_is_set_under_reduced_motion_too()
    {
        var sequence = Sequence(reduced: true);
        var pulse = new IconHintPulse { Sequence = sequence };

        sequence.Open();
        sequence.Collapse();
        _clock.Advance(0.2);
        sequence.OnFrame(_clock.Now);

        Assert.True(pulse.Classes.Contains(":hinting"));
        Assert.True(sequence.IsHintStatic);
    }

    [Fact]
    public void Pointer_entering_the_icon_ends_the_hint()
    {
        var sequence = Sequence();
        var pulse = new IconHintPulse { Sequence = sequence };
        sequence.Open();
        sequence.Collapse();
        _clock.Advance(0.35);
        sequence.OnFrame(_clock.Now);
        Assert.Equal(CollapseToIconState.Hinting, sequence.State);

        var mouse = new Pointer(Pointer.GetNextFreeId(), PointerType.Mouse, true);
        pulse.RaiseEvent(new PointerEventArgs(InputElement.PointerEnteredEvent, pulse, mouse, null,
            new Point(5, 5), 0, new PointerPointProperties(), KeyModifiers.None));

        Assert.Equal(CollapseToIconState.Idle, sequence.State);
    }

    [Fact]
    public void The_centre_is_given_in_the_coordinates_of_the_visual_asked()
    {
        var pulse = new IconHintPulse { Width = 30, Height = 20 };
        Canvas.SetLeft(pulse, 100);
        Canvas.SetTop(pulse, 40);
        var canvas = new Canvas { Width = 400, Height = 200, Children = { pulse } };
        canvas.Measure(new Size(400, 200));
        canvas.Arrange(new Rect(0, 0, 400, 200));

        var centre = pulse.CenterIn(canvas);

        Assert.Equal(new Point(115, 50), centre);
    }
}
