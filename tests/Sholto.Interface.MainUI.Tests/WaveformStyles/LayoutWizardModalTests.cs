using Avalonia.Input;
using Sholto.Interface.MainUI.Controls.Modal;
using Sholto.Interface.MainUI.ViewModels;

namespace Sholto.Interface.MainUI.Tests.WaveformStyles;

/// <summary>The Layout Wizard as <see cref="IModalContent"/>: per-step chrome, Confirm as Next then Apply,
/// Dismiss as Cancel, and the keys the wizard owns itself.</summary>
public class LayoutWizardModalTests
{
    private readonly WizardRig _rig = new(builtIn: 6, user: 2);

    private LayoutWizardViewModel Wizard => _rig.Wizard;

    [Fact]
    public void Step_one_chrome_is_the_theme_step()
    {
        _rig.Open();
        Assert.Equal("LAYOUT WIZARD · THEME", Wizard.Eyebrow);
        Assert.Equal(ModalTone.Accent, Wizard.Tone);
        Assert.Equal("Choose a theme", Wizard.Title);
        Assert.StartsWith("Pick a theme to try it on", Wizard.Subtitle);
        Assert.Equal(ModalWidth.Wide, Wizard.Width);
        Assert.Equal(new ModalButtons("Cancel", "← Back", "Next →"), Wizard.Buttons);
        Assert.False(Wizard.CanGoBack);
        Assert.True(Wizard.CanConfirm);
        Assert.Equal(ModalScrimClick.Dismisses, Wizard.ScrimClick);
        Assert.False(Wizard.CapturesText);
        Assert.Contains("Enter next", Wizard.KeyHint);
    }

    [Fact]
    public void Step_two_chrome_is_the_waveform_step()
    {
        _rig.OpenOnWaveform();
        Assert.Equal("LAYOUT WIZARD · WAVEFORM", Wizard.Eyebrow);
        Assert.Equal("Choose your waveform style", Wizard.Title);
        Assert.StartsWith("Applies to both decks", Wizard.Subtitle);
        Assert.Equal(new ModalButtons("Cancel", "← Back", "Apply"), Wizard.Buttons);
        Assert.True(Wizard.CanGoBack);
        Assert.True(Wizard.CanConfirm);
        Assert.Contains("Enter apply", Wizard.KeyHint);
    }

    [Fact]
    public void Changing_step_raises_each_chrome_property()
    {
        _rig.Open();
        var changes = new List<string?>();
        Wizard.PropertyChanged += (_, e) => changes.Add(e.PropertyName);
        Wizard.Next();
        foreach (var name in new[]
                 {
                     nameof(IModalContent.Eyebrow), nameof(IModalContent.Title), nameof(IModalContent.Subtitle),
                     nameof(IModalContent.KeyHint), nameof(IModalContent.Buttons), nameof(IModalContent.CanGoBack),
                 })
            Assert.Contains(name, changes);
    }

    [Fact]
    public void Confirm_is_Next_on_the_theme_step_then_Apply()
    {
        _rig.Open();
        Wizard.SelectTheme(Wizard.BuiltInThemes[0]);
        Wizard.Confirm();
        Assert.Equal(LayoutWizardStep.Waveform, Wizard.Step);
        Assert.True(Wizard.IsOpen);
        Assert.Empty(_rig.SentStyles.Received);
        Wizard.Select(Wizard.Options.First(o => !o.IsCurrent));

        Wizard.Confirm();
        Assert.False(Wizard.IsOpen);
        Assert.Single(_rig.SentStyles.Received);
        Assert.Single(_rig.SentThemes.Received);
    }

    [Fact]
    public void Back_goes_to_the_theme_step_only_from_the_waveform_step()
    {
        _rig.Open();
        Wizard.Back();
        Assert.Equal(LayoutWizardStep.Theme, Wizard.Step);
        Wizard.Confirm();
        Wizard.Back();
        Assert.Equal(LayoutWizardStep.Theme, Wizard.Step);
    }

    [Fact]
    public void Dismiss_cancels_and_sends_nothing()
    {
        _rig.Open();
        Wizard.SelectTheme(Wizard.BuiltInThemes[0]);
        Wizard.Dismiss();
        Assert.False(Wizard.IsOpen);
        Assert.Empty(_rig.SentStyles.Received);
        Assert.Empty(_rig.SentThemes.Received);
    }

    [Fact]
    public void Arrow_keys_move_the_theme_selection_and_are_handled()
    {
        _rig.Open();
        var start = Wizard.SelectedTheme;
        Assert.True(Wizard.HandleKey(Key.Right, KeyModifiers.None));
        Assert.NotSame(start, Wizard.SelectedTheme);
        Assert.True(Wizard.HandleKey(Key.Left, KeyModifiers.None));
        Assert.Same(start, Wizard.SelectedTheme);
        Assert.True(Wizard.HandleKey(Key.Down, KeyModifiers.None));
        var below = Wizard.SelectedTheme;
        Assert.NotSame(start, below);
        Assert.True(Wizard.HandleKey(Key.Up, KeyModifiers.None));
        Assert.NotSame(below, Wizard.SelectedTheme);
    }

    [Fact]
    public void Home_and_End_jump_to_the_first_and_last_theme()
    {
        _rig.Open();
        Assert.True(Wizard.HandleKey(Key.End, KeyModifiers.None));
        Assert.Same(Wizard.UserThemes[^1], Wizard.SelectedTheme);
        Assert.True(Wizard.HandleKey(Key.Home, KeyModifiers.None));
        Assert.Same(Wizard.BuiltInThemes[0], Wizard.SelectedTheme);
    }

    [Fact]
    public void Digits_pick_a_style_on_the_waveform_step_and_do_nothing_on_the_theme_step()
    {
        _rig.Open();
        var theme = Wizard.SelectedTheme;
        var style = Wizard.Selected;
        Assert.True(Wizard.HandleKey(Key.D2, KeyModifiers.None));
        Assert.Same(theme, Wizard.SelectedTheme);
        Assert.Same(style, Wizard.Selected);

        Wizard.Next();
        Assert.True(Wizard.HandleKey(Key.D2, KeyModifiers.None));
        Assert.Same(Wizard.Options[1], Wizard.Selected);
        Assert.True(Wizard.HandleKey(Key.NumPad1, KeyModifiers.None));
        Assert.Same(Wizard.Options[0], Wizard.Selected);
    }

    [Theory]
    [InlineData(Key.Escape)]
    [InlineData(Key.Enter)]
    [InlineData(Key.Back)]
    [InlineData(Key.A)]
    public void Esc_Enter_Backspace_and_other_keys_are_left_to_the_router(Key key)
    {
        _rig.Open();
        Assert.False(Wizard.HandleKey(key, KeyModifiers.None));
        Assert.True(Wizard.IsOpen);
        Assert.Equal(LayoutWizardStep.Theme, Wizard.Step);
    }

    [Fact]
    public void Through_the_router_Enter_Enter_applies_and_Esc_reverts()
    {
        var router = new ModalKeyRouter();
        _rig.Open();
        Assert.True(router.Route(Wizard, Key.Enter, KeyModifiers.None));
        Assert.Equal(LayoutWizardStep.Waveform, Wizard.Step);
        Assert.True(router.Route(Wizard, Key.Back, KeyModifiers.None));
        Assert.Equal(LayoutWizardStep.Theme, Wizard.Step);
        Assert.True(router.Route(Wizard, Key.Back, KeyModifiers.None));   // step 1: swallowed, no-op
        Assert.Equal(LayoutWizardStep.Theme, Wizard.Step);
        Assert.True(router.Route(Wizard, Key.Escape, KeyModifiers.None));
        Assert.False(Wizard.IsOpen);
        Assert.Empty(_rig.SentThemes.Received);
    }
}
