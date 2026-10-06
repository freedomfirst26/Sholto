using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Presenters;
using Avalonia.Input;
using Avalonia.Interactivity;
using Sholto.Interface.MainUI.Controls.CollapseToIcon;
using Sholto.Interface.MainUI.Controls.Modal;

namespace Sholto.Interface.MainUI.Tests.Modal;

/// <summary>The shell as a control: built from its own XAML, driven through its public properties and events.</summary>
[Collection(AvaloniaXamlCollection.Name)]
public class ModalShellTests
{
    public ModalShellTests() => AvaloniaTestApp.EnsureStarted();

    private static ModalShell Shell(FakeModalContent modal) => new() { Modal = modal };

    private static Button ButtonNamed(ModalShell shell, string name) => shell.FindControl<Button>(name)!;

    private static PointerPressedEventArgs Press(Interactive source)
    {
        var pointer = new Pointer(Pointer.GetNextFreeId(), PointerType.Mouse, true);
        var root = new Border();
        return new PointerPressedEventArgs(source, pointer, root, default, 0,
            new PointerPointProperties(RawInputModifiers.LeftMouseButton, PointerUpdateKind.LeftButtonPressed),
            KeyModifiers.None) { RoutedEvent = InputElement.PointerPressedEvent };
    }

    [Fact]
    public void The_shell_builds_from_its_xaml()
    {
        var shell = new ModalShell();
        Assert.NotNull(shell.FindControl<Border>("Panel"));
        Assert.NotNull(shell.FindControl<Grid>("Scrim"));
    }

    [Fact]
    public void A_wizard_modal_renders_three_buttons_in_order_ghost_secondary_primary()
    {
        var shell = Shell(new FakeModalContent { Buttons = new ModalButtons("Cancel", "← Back", "Apply") });
        var dismiss = ButtonNamed(shell, "DismissButton");
        var back = ButtonNamed(shell, "BackButton");
        var primary = ButtonNamed(shell, "PrimaryButton");

        Assert.Equal("Cancel", dismiss.Content);
        Assert.Equal("← Back", back.Content);
        Assert.Equal("Apply", primary.Content);
        Assert.True(dismiss.IsVisible && back.IsVisible && primary.IsVisible);
        Assert.Equal(0, Grid.GetColumn(dismiss));
        Assert.Equal(2, Grid.GetColumn(back));
        Assert.Equal(3, Grid.GetColumn(primary));
        Assert.True(dismiss.Classes.Contains("ghost"));
        Assert.True(back.Classes.Contains("secondary"));
        Assert.True(primary.Classes.Contains("primary"));
    }

    [Fact]
    public void A_close_only_modal_renders_one_secondary_button_in_the_right_hand_column()
    {
        var shell = Shell(new FakeModalContent { Buttons = new ModalButtons("Close", null, null) });
        var dismiss = ButtonNamed(shell, "DismissButton");

        Assert.Equal("Close", dismiss.Content);
        Assert.Equal(3, Grid.GetColumn(dismiss));
        Assert.True(dismiss.Classes.Contains("secondary"));
        Assert.False(dismiss.Classes.Contains("ghost"));
        Assert.False(ButtonNamed(shell, "BackButton").IsVisible);
        Assert.False(ButtonNamed(shell, "PrimaryButton").IsVisible);
    }

    [Fact]
    public void A_primary_without_back_has_dismiss_left_and_primary_right()
    {
        var shell = Shell(new FakeModalContent { Buttons = new ModalButtons("Cancel", null, "Use device") });
        Assert.Equal(0, Grid.GetColumn(ButtonNamed(shell, "DismissButton")));
        Assert.False(ButtonNamed(shell, "BackButton").IsVisible);
        Assert.True(ButtonNamed(shell, "PrimaryButton").IsVisible);
    }

    [Fact]
    public void Back_and_primary_enabled_state_follow_the_content_after_a_property_change()
    {
        var modal = new FakeModalContent { Buttons = new ModalButtons("Cancel", "← Back", "Apply"), CanGoBack = false, CanConfirm = false };
        var shell = Shell(modal);
        Assert.False(ButtonNamed(shell, "BackButton").IsEnabled);
        Assert.False(ButtonNamed(shell, "PrimaryButton").IsEnabled);

        modal.CanGoBack = true;
        modal.Raise(nameof(IModalContent.CanGoBack));
        Assert.True(ButtonNamed(shell, "BackButton").IsEnabled);
        Assert.False(ButtonNamed(shell, "PrimaryButton").IsEnabled);

        modal.CanConfirm = true;
        modal.Raise(nameof(IModalContent.CanConfirm));
        Assert.True(ButtonNamed(shell, "PrimaryButton").IsEnabled);
    }

    [Fact]
    public void Changing_the_buttons_while_open_relabels_them()
    {
        var modal = new FakeModalContent { Buttons = new ModalButtons("Cancel", "← Back", "Next →") };
        var shell = Shell(modal);
        modal.Buttons = new ModalButtons("Cancel", "← Back", "Apply");
        modal.Raise(nameof(IModalContent.Buttons));
        Assert.Equal("Apply", ButtonNamed(shell, "PrimaryButton").Content);
    }

    [Fact]
    public void Clicking_each_button_calls_the_matching_method()
    {
        var modal = new FakeModalContent { Buttons = new ModalButtons("Cancel", "← Back", "Apply") };
        var shell = Shell(modal);

        ButtonNamed(shell, "DismissButton").RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
        ButtonNamed(shell, "BackButton").RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
        ButtonNamed(shell, "PrimaryButton").RaiseEvent(new RoutedEventArgs(Button.ClickEvent));

        Assert.Equal(1, modal.DismissCalls);
        Assert.Equal(1, modal.BackCalls);
        Assert.Equal(1, modal.ConfirmCalls);
    }

    [Theory]
    [InlineData(ModalScrimClick.Dismisses, 1)]
    [InlineData(ModalScrimClick.Ignored, 0)]
    public void A_scrim_press_dismisses_only_when_the_content_says_so_and_is_always_handled(ModalScrimClick mode, int dismissals)
    {
        var modal = new FakeModalContent { ScrimClick = mode };
        var shell = Shell(modal);
        var scrim = shell.FindControl<Grid>("Scrim")!;
        var args = Press(scrim);

        scrim.RaiseEvent(args);

        Assert.Equal(dismissals, modal.DismissCalls);
        Assert.True(args.Handled);
    }

    [Fact]
    public void A_panel_press_does_not_dismiss_and_does_not_reach_the_scrim()
    {
        var modal = new FakeModalContent();
        var shell = Shell(modal);
        var panel = shell.FindControl<Border>("Panel")!;
        var args = Press(panel);

        panel.RaiseEvent(args);

        Assert.Equal(0, modal.DismissCalls);
        Assert.True(args.Handled);
    }

    [Fact]
    public void Visibility_follows_IsOpen_when_the_shell_shows_itself()
    {
        var modal = new FakeModalContent { IsOpen = false };
        var shell = Shell(modal);
        Assert.False(shell.IsVisible);

        modal.IsOpen = true;
        modal.Raise(nameof(IModal.IsOpen));
        Assert.True(shell.IsVisible);

        modal.IsOpen = false;
        modal.Raise(nameof(IModal.IsOpen));
        Assert.False(shell.IsVisible);
    }

    [Fact]
    public void Visibility_stays_true_when_an_outer_host_gates_it()
    {
        var modal = new FakeModalContent { IsOpen = false };
        var shell = new ModalShell { Modal = modal, ShowsItself = false };
        Assert.True(shell.IsVisible);

        modal.Raise(nameof(IModal.IsOpen));
        Assert.True(shell.IsVisible);
    }

    [Fact]
    public void The_subtitle_row_collapses_when_the_subtitle_is_null()
    {
        var modal = new FakeModalContent { Subtitle = "Something" };
        var shell = Shell(modal);
        var subtitle = shell.FindControl<TextBlock>("SubtitleText")!;
        Assert.True(subtitle.IsVisible);

        modal.Subtitle = null;
        modal.Raise(nameof(IModalContent.Subtitle));
        Assert.False(subtitle.IsVisible);
    }

    [Fact]
    public void Tone_attention_sets_the_eyebrows_attention_class()
    {
        var modal = new FakeModalContent { Tone = ModalTone.Accent };
        var shell = Shell(modal);
        var eyebrow = shell.FindControl<TextBlock>("EyebrowText")!;
        Assert.False(eyebrow.Classes.Contains("attention"));

        modal.Tone = ModalTone.Attention;
        modal.Raise(nameof(IModalContent.Tone));
        Assert.True(eyebrow.Classes.Contains("attention"));
    }

    [Theory]
    [InlineData(ModalWidth.Narrow, 440)]
    [InlineData(ModalWidth.Regular, 600)]
    [InlineData(ModalWidth.Wide, 900)]
    public void The_panel_width_follows_the_content_width(ModalWidth width, double pixels)
    {
        var shell = Shell(new FakeModalContent { Width = width });
        Assert.Equal(pixels, shell.FindControl<Border>("Panel")!.Width);
    }

    [Fact]
    public void The_body_and_header_accessory_are_hosted()
    {
        var body = new TextBlock();
        var accessory = new TextBlock();
        var shell = new ModalShell { Modal = new FakeModalContent(), Body = body, HeaderAccessory = accessory };
        Assert.Same(body, shell.FindControl<ContentPresenter>("BodyPresenter")!.Content);
        Assert.Same(accessory, shell.FindControl<ContentPresenter>("AccessoryPresenter")!.Content);
    }

    [Fact]
    public void FocusOnOpen_finds_the_marked_element_inside_the_body()
    {
        var box = new TextBox();
        ModalShell.SetFocusOnOpen(box, true);
        var shell = new ModalShell { Modal = new FakeModalContent(), Body = new StackPanel { Children = { new TextBlock(), box } } };
        Assert.Same(box, shell.FindFocusTarget());
    }

    [Fact]
    public void FocusOnOpen_finds_nothing_when_nothing_is_marked()
    {
        var shell = new ModalShell { Modal = new FakeModalContent(), Body = new StackPanel { Children = { new TextBox() } } };
        Assert.Null(shell.FindFocusTarget());
    }

    [Fact]
    public void A_shell_that_does_not_show_itself_follows_an_icon_docked_container_through_collapse()
    {
        var clock = new FakeFrameClock();
        var sequence = new CollapseToIconSequence(clock, new FixedMotionPreference(false),
            new CollapseToIconOptions("test", new CollapseToIconTimingsFactory().Standard()), new AlwaysHintPolicy());
        var shell = new ModalShell { Modal = new FakeModalContent { IsOpen = true }, ShowsItself = false };
        var container = new IconDockedContainer { Sequence = sequence, Content = shell };
        Assert.False(container.IsVisible);

        sequence.Open();
        Assert.True(container.IsVisible);
        Assert.True(shell.IsVisible);

        sequence.Collapse();
        Assert.True(container.IsVisible);
        Assert.True(shell.IsVisible);

        clock.Advance(0.4);
        sequence.OnFrame(clock.Now);
        Assert.Equal(CollapseToIconState.Hinting, sequence.State);
        Assert.False(container.IsVisible);
        Assert.True(shell.IsVisible);
    }
}
