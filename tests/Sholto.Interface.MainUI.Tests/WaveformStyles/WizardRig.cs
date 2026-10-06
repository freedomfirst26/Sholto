using Sholto.Data;
using Sholto.Interface.MainUI.Controls.WaveformStyles;
using Sholto.Interface.MainUI.Theming;
using Sholto.Interface.MainUI.ViewModels;

namespace Sholto.Interface.MainUI.Tests.WaveformStyles;

/// <summary>A Layout Wizard over the real view models, a bus that records what the App would be told, and a
/// catalog of <paramref name="builtIn"/> bundled themes followed by <paramref name="user"/> user ones.</summary>
internal sealed class WizardRig
{
    public WizardRig(int builtIn, int user)
    {
        AvaloniaTestApp.EnsureStarted();
        var real = new ThemeStackFactory().Build().Catalog.All.Where(t => !t.IsUser).ToList();
        var themes = real.Take(builtIn).ToList();
        for (int i = 0; i < user; i++) themes.Add(real[i] with { Name = $"Mine {i + 1}", IsUser = true });
        Catalog = new ThemeCatalog(themes, "/tmp/sholto-test-themes");
        Context = new ThemeContext(Catalog);

        Bus = new DataBus(new ThrowingFailureSink());
        Bus.Register<ChooseWaveformStyle>(SentStyles);
        Bus.Register<ChooseTheme>(SentThemes);
        Styles = new WaveformStylesFactory().Create();
        Style = new WaveformStyleViewModel(Styles, Bus);
        Themes = new ThemeViewModel(Context, Catalog, Bus);
        Wizard = new LayoutWizardViewModel(Style, new WaveformStyleOptionFactory(), new CountingPreviewRenderer(),
            new DemoWaveformFactory(), new WaveformPreviewScroll(new ManualFrameClock()), new ImmediateAppThread(),
            Themes, new ThemeOptionFactory());
    }

    public ThemeCatalog Catalog { get; }
    public ThemeContext Context { get; }
    public DataBus Bus { get; }
    public F9RecordingCommandHandler<ChooseWaveformStyle> SentStyles { get; } = new();
    public F9RecordingCommandHandler<ChooseTheme> SentThemes { get; } = new();
    public IWaveformStyles Styles { get; }
    public WaveformStyleViewModel Style { get; }
    public ThemeViewModel Themes { get; }
    public LayoutWizardViewModel Wizard { get; }

    public void Open() => Wizard.Open(new TestWaveformPalette().Create());

    /// <summary>Open (on the theme step) and go on to the waveform step.</summary>
    public void OpenOnWaveform()
    {
        Open();
        Wizard.Next();
    }

    public string SelectedName => Wizard.SelectedTheme!.Name;
}
