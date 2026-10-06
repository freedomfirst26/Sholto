using Sholto.Data;
using Sholto.Interface.MainUI.Controls.WaveformStyles;
using Sholto.Interface.MainUI.Theming;
using Sholto.Interface.MainUI.ViewModels;

namespace Sholto.Interface.MainUI.Tests.WaveformStyles;

public class LayoutWizardViewModelTests
{
    private readonly DataBus _bus = new(new ThrowingFailureSink());
    private readonly F9RecordingCommandHandler<ChooseWaveformStyle> _sent = new();
    private readonly IWaveformStyles _styles = new WaveformStylesFactory().Create();
    private readonly WaveformStyleViewModel _style;
    private readonly CountingPreviewRenderer _previews = new();
    private readonly ManualFrameClock _clock = new();
    private readonly WaveformPreviewScroll _scroll;
    private readonly LayoutWizardViewModel _wizard;

    public LayoutWizardViewModelTests()
    {
        AvaloniaTestApp.EnsureStarted();
        _bus.Register<ChooseWaveformStyle>(_sent);
        _style = new WaveformStyleViewModel(_styles, _bus);
        _scroll = new WaveformPreviewScroll(_clock);
        var themes = new ThemeStackFactory().Build();
        _wizard = new LayoutWizardViewModel(_style, new WaveformStyleOptionFactory(), _previews,
            new DemoWaveformFactory(), _scroll, new ImmediateAppThread(),
            new ThemeViewModel(themes.Context, themes.Catalog, _bus), new ThemeOptionFactory());
    }

    // The wizard opens on the theme step; these tests are about the waveform step.
    private void Open()
    {
        _wizard.Open(new TestWaveformPalette().Create());
        _wizard.Next();
    }

    [Fact]
    public void Opens_with_one_card_per_style_and_the_chosen_one_selected_and_tagged_current()
    {
        _style.Restore("rgb");
        Open();

        Assert.True(_wizard.IsOpen);
        Assert.Equal(["3-BAND", "RGB"], _wizard.Options.Select(o => o.Name));
        Assert.Equal(["1", "2"], _wizard.Options.Select(o => o.Key));
        Assert.Same(_wizard.Options[1], _wizard.Selected);
        Assert.True(_wizard.Options[1].IsSelected);
        Assert.Equal("CURRENT", _wizard.Options[1].Tag);
        Assert.Null(_wizard.Options[0].Tag);
        Assert.Equal(["Low", "Mid", "High"], _wizard.Options[0].Legend.Select(l => l.Label));
    }

    [Fact]
    public void Rgb_is_tagged_new_while_three_band_is_current()
    {
        Open();
        Assert.Equal("CURRENT", _wizard.Options[0].Tag);
        Assert.Equal("NEW", _wizard.Options[1].Tag);
    }

    [Fact]
    public void Picking_previews_on_the_decks_without_saving()
    {
        Open();
        _wizard.Move(+1);

        Assert.Same(_styles.ById("rgb"), _style.Shown);
        Assert.Same(_styles.Default, _style.Chosen);
        Assert.Empty(_sent.Received);
        Assert.False(_wizard.Options[0].IsSelected);
        Assert.True(_wizard.Options[1].IsSelected);
    }

    [Fact]
    public void Apply_chooses_the_selected_style_and_closes()
    {
        Open();
        _wizard.SelectIndex(1);
        _wizard.Apply();

        Assert.False(_wizard.IsOpen);
        Assert.Same(_styles.ById("rgb"), _style.Chosen);
        Assert.Equal(["rgb"], _sent.Received.Select(c => c.Id));
    }

    [Fact]
    public void Cancel_reverts_the_decks_to_the_style_in_use_when_opened()
    {
        Open();
        _wizard.SelectIndex(1);
        _wizard.Cancel();

        Assert.False(_wizard.IsOpen);
        Assert.Same(_styles.Default, _style.Shown);
        Assert.Empty(_sent.Received);
    }

    [Fact]
    public void Selection_is_clamped_and_ignored_while_closed()
    {
        Open();
        _wizard.Move(-1);
        Assert.Same(_wizard.Options[0], _wizard.Selected);
        _wizard.Move(+5);
        Assert.Same(_wizard.Options[1], _wizard.Selected);
        _wizard.SelectIndex(7);
        Assert.Same(_wizard.Options[1], _wizard.Selected);

        _wizard.Cancel();
        _wizard.SelectIndex(1);
        _wizard.Apply();
        Assert.Empty(_sent.Received);
    }

    [Fact]
    public async Task Each_card_gets_a_preview_render_of_the_demo_without_any_deck_track()
    {
        Open();
        for (int i = 0; i < 100 && _previews.Calls < 2; i++) await Task.Delay(20);
        Assert.Equal(2, _previews.Calls);
        Assert.All(_wizard.Options, o => Assert.Same(_scroll, o.Scroll));
    }

    [Fact]
    public void The_previews_scroll_only_while_the_wizard_is_open()
    {
        Assert.False(_scroll.IsRunning);
        Open();
        Assert.True(_scroll.IsRunning);
        Assert.True(_scroll.Position > 0);

        _wizard.Cancel();
        Assert.False(_scroll.IsRunning);
    }
}
