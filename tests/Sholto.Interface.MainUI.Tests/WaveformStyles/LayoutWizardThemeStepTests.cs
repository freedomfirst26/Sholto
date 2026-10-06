using Avalonia.Media;
using Sholto.Interface.MainUI.ViewModels;

namespace Sholto.Interface.MainUI.Tests.WaveformStyles;

public class LayoutWizardThemeStepTests
{
    // 6 bundled themes (a row of 4 and a partial row of 2) then 2 user themes; the app starts on Silence Groove,
    // which is the 4th bundled theme in manifest order (index 3).
    private readonly WizardRig _rig = new(builtIn: 6, user: 2);

    private LayoutWizardViewModel Wizard => _rig.Wizard;

    [Fact]
    public void Opens_on_the_theme_step_and_Next_and_Back_move_between_the_steps()
    {
        _rig.Open();
        Assert.Equal(LayoutWizardStep.Theme, Wizard.Step);
        Assert.True(Wizard.IsThemeStep);
        Assert.Equal("LAYOUT WIZARD · THEME", Wizard.Eyebrow);

        Wizard.Back();
        Assert.Equal(LayoutWizardStep.Theme, Wizard.Step);

        Wizard.Next();
        Assert.Equal(LayoutWizardStep.Waveform, Wizard.Step);
        Assert.True(Wizard.IsWaveformStep);
        Assert.False(Wizard.IsThemeStep);
        Assert.Equal("LAYOUT WIZARD · WAVEFORM", Wizard.Eyebrow);

        Wizard.Next();
        Assert.Equal(LayoutWizardStep.Waveform, Wizard.Step);

        Wizard.Back();
        Assert.Equal(LayoutWizardStep.Theme, Wizard.Step);
    }

    [Fact]
    public void Each_step_has_its_own_key_hint()
    {
        _rig.Open();
        var first = Wizard.KeyHint;
        Assert.Contains("Enter next", first);
        Assert.Contains("Home End", first);
        Wizard.Next();
        Assert.NotEqual(first, Wizard.KeyHint);
        Assert.Contains("Enter apply", Wizard.KeyHint);
        Assert.Contains("Backspace back", Wizard.KeyHint);
    }

    [Fact]
    public void The_done_step_names_the_theme_picked_on_step_one()
    {
        _rig.Open();
        Wizard.SelectTheme(Wizard.BuiltInThemes[0]);
        Assert.Equal(Wizard.BuiltInThemes[0].Name, Wizard.ThemeSummary);
    }

    [Fact]
    public void Reopening_starts_on_the_theme_step_again()
    {
        _rig.OpenOnWaveform();
        Wizard.Cancel();
        _rig.Open();
        Assert.True(Wizard.IsThemeStep);
    }

    [Fact]
    public void The_theme_cards_list_bundled_themes_then_user_themes_with_counts_and_the_folder()
    {
        _rig.Open();
        Assert.Equal(6, Wizard.BuiltInThemes.Count);
        Assert.Equal(["Mine 1", "Mine 2"], Wizard.UserThemes.Select(o => o.Name));
        Assert.True(Wizard.HasUserThemes);
        Assert.Equal("BUILT-IN · 6", Wizard.BuiltInHeading);
        Assert.Equal("YOUR THEMES · 2", Wizard.UserHeading);
        Assert.Equal("/tmp/sholto-test-themes", Wizard.UserThemesDirectory);
        Assert.All(Wizard.UserThemes, o => Assert.True(o.IsUser));
        Assert.All(Wizard.BuiltInThemes, o => Assert.False(o.IsUser));
    }

    [Fact]
    public void With_no_user_themes_the_group_is_empty()
    {
        var rig = new WizardRig(builtIn: 5, user: 0);
        rig.Open();
        Assert.False(rig.Wizard.HasUserThemes);
        Assert.Equal("YOUR THEMES · 0", rig.Wizard.UserHeading);
        Assert.Equal("BUILT-IN · 5", rig.Wizard.BuiltInHeading);
    }

    [Fact]
    public void The_theme_in_use_when_opened_is_selected_and_tagged_current()
    {
        _rig.Open();
        var current = Wizard.BuiltInThemes.Single(o => o.IsCurrent);
        Assert.Equal("Silence Groove", current.Name);
        Assert.Same(current, Wizard.SelectedTheme);
        Assert.True(current.IsSelected);
        Assert.Single(Wizard.BuiltInThemes.Concat(Wizard.UserThemes), o => o.IsCurrent);
    }

    [Fact]
    public void Selecting_a_theme_tries_it_on_the_app_without_saving_anything()
    {
        _rig.Open();
        var current = Wizard.SelectedTheme!;
        var other = Wizard.BuiltInThemes[0];

        Wizard.SelectTheme(other);

        Assert.Equal(other.Theme, _rig.Themes.Shown);
        Assert.Equal(other.Theme, _rig.Context.Current);
        Assert.Equal(current.Theme, _rig.Themes.Chosen);
        Assert.Empty(_rig.SentThemes.Received);
        Assert.Empty(_rig.SentStyles.Received);
        Assert.True(other.IsSelected);
        Assert.False(current.IsSelected);
        Assert.True(current.IsCurrent);
        Assert.False(other.IsCurrent);
    }

    [Fact]
    public void Apply_saves_both_the_theme_and_the_waveform_style()
    {
        _rig.Open();
        Wizard.SelectTheme(Wizard.BuiltInThemes[0]);
        Wizard.Next();
        Wizard.SelectIndex(1);
        Wizard.Apply();

        Assert.False(Wizard.IsOpen);
        Assert.Equal(["rgb"], _rig.SentStyles.Received.Select(c => c.Id));
        Assert.Equal([Wizard.BuiltInThemes[0].Name], _rig.SentThemes.Received.Select(c => c.Name));
        Assert.Equal(Wizard.BuiltInThemes[0].Theme, _rig.Themes.Chosen);
        Assert.Same(_rig.Styles.ById("rgb"), _rig.Style.Chosen);
    }

    [Fact]
    public void Picks_made_on_step_one_are_not_saved_when_going_on_or_back()
    {
        _rig.Open();
        Wizard.SelectTheme(Wizard.BuiltInThemes[0]);
        Wizard.Next();
        Wizard.Back();

        Assert.Empty(_rig.SentThemes.Received);
        Assert.NotEqual(Wizard.BuiltInThemes[0].Theme, _rig.Themes.Chosen);
        Assert.Equal(Wizard.BuiltInThemes[0].Theme, _rig.Themes.Shown);
    }

    [Fact]
    public void Style_picks_on_step_two_are_not_saved_until_Apply()
    {
        _rig.OpenOnWaveform();
        Wizard.SelectIndex(1);

        Assert.Empty(_rig.SentStyles.Received);
        Assert.Same(_rig.Styles.Default, _rig.Style.Chosen);
        Assert.Same(_rig.Styles.ById("rgb"), _rig.Style.Shown);
    }

    [Fact]
    public void Cancel_on_the_waveform_step_reverts_both_the_theme_and_the_waveform_style()
    {
        var before = _rig.Themes.Shown;
        _rig.Open();
        Wizard.SelectTheme(Wizard.UserThemes[1]);
        Wizard.Next();
        Wizard.SelectIndex(1);
        Assert.NotEqual(before, _rig.Context.Current);

        Wizard.Cancel();

        Assert.False(Wizard.IsOpen);
        Assert.Equal(before, _rig.Themes.Shown);
        Assert.Equal(before, _rig.Context.Current);
        Assert.Same(_rig.Styles.Default, _rig.Style.Shown);
        Assert.Empty(_rig.SentThemes.Received);
        Assert.Empty(_rig.SentStyles.Received);
    }

    [Fact]
    public void Cancel_reverts_to_the_saved_theme_not_the_one_it_was_tried_from()
    {
        _rig.Themes.Choose(_rig.Catalog.All[1]);
        _rig.SentThemes.Received.Clear();
        _rig.Open();
        Assert.Equal(_rig.Catalog.All[1].Name, Wizard.SelectedTheme!.Name);

        Wizard.SelectTheme(Wizard.BuiltInThemes[4]);
        Wizard.Cancel();

        Assert.Equal(_rig.Catalog.All[1], _rig.Themes.Shown);
        Assert.Empty(_rig.SentThemes.Received);
    }

    [Fact]
    public void Applying_the_untouched_wizard_changes_and_sends_nothing_new()
    {
        _rig.Open();
        Wizard.Apply();
        Assert.Empty(_rig.SentThemes.Received);
        Assert.Empty(_rig.SentStyles.Received);
    }

    [Fact]
    public void Left_and_right_walk_the_whole_grid_and_stop_at_both_ends()
    {
        _rig.Open();
        Wizard.MoveToEdge(false);
        Assert.Equal(Wizard.BuiltInThemes[0].Name, _rig.SelectedName);
        Wizard.Move(-1);
        Assert.Equal(Wizard.BuiltInThemes[0].Name, _rig.SelectedName);

        for (int i = 0; i < 5; i++) Wizard.Move(+1);
        Assert.Equal(Wizard.BuiltInThemes[5].Name, _rig.SelectedName);
        Wizard.Move(+1);
        Assert.Equal("Mine 1", _rig.SelectedName);

        Wizard.MoveToEdge(true);
        Assert.Equal("Mine 2", _rig.SelectedName);
        Wizard.Move(+1);
        Assert.Equal("Mine 2", _rig.SelectedName);
    }

    [Fact]
    public void Down_and_up_move_a_row_of_four_and_stop_at_the_top_edge()
    {
        _rig.Open();
        Wizard.SelectTheme(Wizard.BuiltInThemes[1]);

        Wizard.MoveRows(-1);
        Assert.Equal(Wizard.BuiltInThemes[1].Name, _rig.SelectedName);

        Wizard.MoveRows(+1);
        Assert.Equal(Wizard.BuiltInThemes[5].Name, _rig.SelectedName);
    }

    [Fact]
    public void Down_from_the_last_built_in_row_enters_the_user_group_in_the_same_column_and_up_returns()
    {
        _rig.Open();
        Wizard.SelectTheme(Wizard.BuiltInThemes[5]);   // column 1 of the partial second row

        Wizard.MoveRows(+1);
        Assert.Equal("Mine 2", _rig.SelectedName);

        Wizard.MoveRows(+1);   // bottom edge
        Assert.Equal("Mine 2", _rig.SelectedName);

        Wizard.MoveRows(-1);
        Assert.Equal(Wizard.BuiltInThemes[5].Name, _rig.SelectedName);
    }

    [Fact]
    public void Down_into_a_partial_row_lands_on_its_last_card()
    {
        _rig.Open();
        Wizard.SelectTheme(Wizard.BuiltInThemes[3]);   // column 3, but the second row only has 2 cards
        Wizard.MoveRows(+1);
        Assert.Equal(Wizard.BuiltInThemes[5].Name, _rig.SelectedName);
    }

    [Fact]
    public void Down_at_the_bottom_with_no_user_themes_stays_put()
    {
        var rig = new WizardRig(builtIn: 6, user: 0);
        rig.Open();
        rig.Wizard.SelectTheme(rig.Wizard.BuiltInThemes[5]);
        rig.Wizard.MoveRows(+1);
        Assert.Equal(rig.Wizard.BuiltInThemes[5].Name, rig.SelectedName);
    }

    [Fact]
    public void Home_and_End_pick_the_first_and_last_theme()
    {
        _rig.Open();
        Wizard.MoveToEdge(true);
        Assert.Equal("Mine 2", _rig.SelectedName);
        Assert.Equal(_rig.Catalog.All[^1], _rig.Context.Current);
        Wizard.MoveToEdge(false);
        Assert.Equal(Wizard.BuiltInThemes[0].Name, _rig.SelectedName);
        Assert.Equal(_rig.Catalog.All[0], _rig.Context.Current);
    }

    [Fact]
    public void Row_moves_do_nothing_on_the_waveform_step()
    {
        _rig.OpenOnWaveform();
        var before = Wizard.SelectedTheme;
        Wizard.MoveRows(+1);
        Assert.Same(Wizard.Options[0], Wizard.Selected);
        Assert.Same(before, Wizard.SelectedTheme);
    }

    [Fact]
    public void Number_keys_pick_a_style_only_on_the_waveform_step()
    {
        _rig.Open();
        Wizard.SelectIndex(1);   // on the theme step this means theme #2
        Assert.Same(Wizard.BuiltInThemes[1], Wizard.SelectedTheme);
        Assert.Same(Wizard.Options[0], Wizard.Selected);
    }

    [Fact]
    public void The_theme_cards_are_independent_of_the_waveform_style_picked_on_step_two()
    {
        _rig.OpenOnWaveform();
        Wizard.SelectIndex(1);
        Wizard.Back();
        Assert.Equal(6, Wizard.BuiltInThemes.Count);
        Assert.All(Wizard.BuiltInThemes.Concat(Wizard.UserThemes), o => Assert.NotNull(o.Theme.Waveform));
        Assert.Equal(_rig.Catalog.All[0].Waveform, Wizard.BuiltInThemes[0].Theme.Waveform);
    }

    [Fact]
    public void Selecting_a_theme_recolours_the_style_legends_with_that_themes_colours()
    {
        _rig.Open();
        var opened = new TestWaveformPalette().Create();
        var other = Wizard.BuiltInThemes.First(o =>
            o.Theme.Waveform.Low != o.Theme.Waveform.RgbLow && o.Theme.Waveform.Low != opened.Low
            && o.Theme.Waveform.RgbLow != opened.RgbLow);

        Wizard.SelectTheme(other);

        var threeBand = Wizard.Options.Single(o => o.Strategy.Id == "three-band");
        var rgb = Wizard.Options.Single(o => o.Strategy.Id == "rgb");
        Assert.Equal(other.Theme.Waveform.Low, ((SolidColorBrush)threeBand.Legend[0].Swatch).Color);
        Assert.Equal(other.Theme.Waveform.RgbLow, ((SolidColorBrush)rgb.Legend[0].Swatch).Color);
    }

    [Fact]
    public void Nothing_moves_while_the_wizard_is_closed()
    {
        _rig.Open();
        Wizard.Cancel();
        var before = _rig.Context.Current;
        Wizard.MoveToEdge(true);
        Wizard.Move(+1);
        Wizard.Next();
        Assert.Equal(before, _rig.Context.Current);
        Assert.True(Wizard.IsThemeStep);
    }
}
