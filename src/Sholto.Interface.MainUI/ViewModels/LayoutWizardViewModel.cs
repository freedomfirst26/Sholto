using System.ComponentModel;
using Avalonia.Input;
using Sholto.Data;
using Sholto.Interface.MainUI.Controls.Modal;
using Sholto.Interface.MainUI.Theming;

namespace Sholto.Interface.MainUI.ViewModels;

/// <inheritdoc />
public sealed class LayoutWizardViewModel(
    IWaveformStyleViewModel waveformStyle,
    IWaveformStyleOptionFactory options,
    IWaveformPreviewRenderer previews,
    IDemoWaveformFactory demo,
    IWaveformPreviewScroll scroll,
    IAppThread appThread,
    IThemeViewModel themes,
    IThemeOptionFactory themeOptions) : ILayoutWizardViewModel
{
    private readonly IWaveformStyleViewModel _waveformStyle = waveformStyle;
    private readonly IWaveformStyleOptionFactory _optionFactory = options;
    private readonly IWaveformPreviewRenderer _previews = previews;
    private readonly IDemoWaveformFactory _demo = demo;
    private readonly IWaveformPreviewScroll _scroll = scroll;
    private readonly IAppThread _appThread = appThread;
    private readonly IThemeViewModel _themes = themes;
    private readonly IThemeOptionFactory _themeOptionFactory = themeOptions;

    // The theme grid has four columns.
    private const int Columns = 4;

    // Where the demo starts: a fifth in, in the groove, so the build and breakdown follow soon after.
    private const double StartFraction = 0.2;

    private readonly ModalButtons _themeButtons = new("Cancel", "← Back", "Next →");
    private readonly ModalButtons _waveformButtons = new("Cancel", "← Back", "Apply");

    private bool _isOpen;
    private IReadOnlyList<WaveformStyleOption> _options = [];
    private WaveformStyleOption? _selected;
    private LayoutWizardStep _step;
    private IReadOnlyList<ThemeOption> _builtIn = [];
    private IReadOnlyList<ThemeOption> _user = [];
    private ThemeOption? _selectedTheme;
    private WaveformPeaks? _peaks;
    // Bumped on every open so a slow preview render from an earlier open is dropped.
    private int _generation;

    public event PropertyChangedEventHandler? PropertyChanged;

    public bool IsOpen
    {
        get => _isOpen;
        private set
        {
            if (_isOpen == value) return;
            _isOpen = value;
            Notify(nameof(IsOpen));
        }
    }

    public LayoutWizardStep Step
    {
        get => _step;
        private set
        {
            if (_step == value) return;
            _step = value;
            Notify(nameof(Step));
            Notify(nameof(IsWaveformStep));
            Notify(nameof(IsThemeStep));
            Notify(nameof(Eyebrow));
            Notify(nameof(Title));
            Notify(nameof(Subtitle));
            Notify(nameof(KeyHint));
            Notify(nameof(Buttons));
            Notify(nameof(CanGoBack));
        }
    }

    public bool IsWaveformStep => _step == LayoutWizardStep.Waveform;

    public bool IsThemeStep => _step == LayoutWizardStep.Theme;

    public string Eyebrow => IsThemeStep ? "LAYOUT WIZARD · THEME" : "LAYOUT WIZARD · WAVEFORM";

    public string KeyHint => IsThemeStep
        ? "← → ↑ ↓  choose  ·  Home End  first / last  ·  Enter next  ·  Esc cancel"
        : "← →  or  1 2  choose  ·  Enter apply  ·  Backspace back  ·  Esc cancel";

    public ModalTone Tone => ModalTone.Accent;

    public string Title => IsThemeStep ? "Choose a theme" : "Choose your waveform style";

    public string? Subtitle => IsThemeStep
        ? "Pick a theme to try it on: the app and this panel retone straight away. Nothing is saved until Apply."
        : "Applies to both decks. Both previews play the same demo track in the theme you picked, so you can compare them directly.";

    public ModalWidth Width => ModalWidth.Wide;

    public ModalButtons Buttons => IsThemeStep ? _themeButtons : _waveformButtons;

    public ModalScrimClick ScrimClick => ModalScrimClick.Dismisses;

    public bool CapturesText => false;

    public bool CanGoBack => IsWaveformStep;

    public bool CanConfirm => true;

    public void Dismiss() => Cancel();

    public void Confirm()
    {
        if (IsThemeStep) Next();
        else Apply();
    }

    /// <summary>Arrows and Home / End move over the cards; 1 / 2 pick a style on the waveform step. Enter,
    /// Backspace and Esc are the router's (Confirm, Back, Dismiss).</summary>
    public bool HandleKey(Key key, KeyModifiers modifiers)
    {
        switch (key)
        {
            case Key.Left: Move(-1); return true;
            case Key.Right: Move(+1); return true;
            case Key.Up: MoveRows(-1); return true;
            case Key.Down: MoveRows(+1); return true;
            case Key.Home: MoveToEdge(false); return true;
            case Key.End: MoveToEdge(true); return true;
            case Key.D1: case Key.NumPad1: if (IsWaveformStep) SelectIndex(0); return true;
            case Key.D2: case Key.NumPad2: if (IsWaveformStep) SelectIndex(1); return true;
            default: return false;
        }
    }

    public string ThemeSummary => _selectedTheme?.Name ?? "";

    public IReadOnlyList<WaveformStyleOption> Options => _options;

    public IReadOnlyList<ThemeOption> BuiltInThemes => _builtIn;

    public IReadOnlyList<ThemeOption> UserThemes => _user;

    public bool HasUserThemes => _user.Count > 0;

    public string BuiltInHeading => $"BUILT-IN · {_builtIn.Count}";

    public string UserHeading => $"YOUR THEMES · {_user.Count}";

    public string UserThemesDirectory => _themes.UserThemesDirectory;

    public ThemeOption? SelectedTheme => _selectedTheme;

    public WaveformStyleOption? Selected => _selected;

    public IWaveformPreviewScroll Scroll => _scroll;

    public void Open(WaveformPalette palette)
    {
        if (IsOpen) return;
        var chosen = _waveformStyle.Chosen;
        // Start from what is chosen, so a leftover preview cannot leak into the wizard.
        _waveformStyle.Preview(chosen);
        var chosenTheme = _themes.Chosen;
        _themes.Preview(chosenTheme);
        Step = LayoutWizardStep.Theme;

        foreach (var old in _options) old.Preview?.Dispose();
        var all = _waveformStyle.All;
        var built = new List<WaveformStyleOption>(all.Count);
        for (int i = 0; i < all.Count; i++)
        {
            var option = _optionFactory.Create(all[i], i, ReferenceEquals(all[i], chosen), palette);
            option.Scroll = _scroll;
            built.Add(option);
        }
        _options = built;
        Notify(nameof(Options));
        SetSelected(built.FirstOrDefault(o => o.IsCurrent) ?? built.FirstOrDefault());

        BuildThemeOptions(chosenTheme);

        var peaks = _peaks = _demo.Peaks;
        RenderPreviews(peaks, palette);
        _scroll.Start(peaks.Min.Length, 1.0 / peaks.SecondsPerPeak,
            peaks.Min.Length * StartFraction);

        IsOpen = true;
    }

    private void BuildThemeOptions(SholtoTheme current)
    {
        var builtIn = new List<ThemeOption>();
        var user = new List<ThemeOption>();
        foreach (var theme in _themes.All)
            (theme.IsUser ? user : builtIn).Add(_themeOptionFactory.Create(theme, theme == current));
        _builtIn = builtIn;
        _user = user;
        Notify(nameof(BuiltInThemes));
        Notify(nameof(UserThemes));
        Notify(nameof(HasUserThemes));
        Notify(nameof(BuiltInHeading));
        Notify(nameof(UserHeading));
        SetSelectedTheme(builtIn.Concat(user).FirstOrDefault(o => o.IsCurrent));
    }

    /// <summary>Re-bake the style previews (a new generation drops any render still in flight).</summary>
    private void RenderPreviews(WaveformPeaks peaks, WaveformPalette palette)
    {
        int generation = ++_generation;
        foreach (var option in _options) RenderPreview(option, peaks, palette, generation);
    }

    private void RenderPreview(WaveformStyleOption option, WaveformPeaks peaks, WaveformPalette palette,
        int generation)
    {
        var style = option.Strategy;
        _ = Task.Run(() =>
        {
            try
            {
                var bitmap = _previews.Render(style, peaks, palette);
                if (bitmap is null) return;
                _appThread.Post(() =>
                {
                    if (generation == _generation) option.Preview = bitmap;
                    else bitmap.Dispose();
                });
            }
            catch (Exception ex) { Console.WriteLine($"[LayoutWizard] preview failed: {ex.Message}"); }
        });
    }

    public void Select(WaveformStyleOption option)
    {
        if (!IsOpen || !_options.Contains(option)) return;
        SetSelected(option);
        _waveformStyle.Preview(option.Strategy);
    }

    public void SelectTheme(ThemeOption option)
    {
        if (!IsOpen || !IsThemeStep || !AllThemes().Contains(option)) return;
        SetSelectedTheme(option);
        _themes.Preview(option.Theme);
        // The style previews wear the theme's waveform colours.
        if (_peaks is { } peaks) RenderPreviews(peaks, option.Theme.Waveform);
        foreach (var card in _options) card.Legend = _optionFactory.Legend(card.Strategy, option.Theme.Waveform);
    }

    public void SelectIndex(int index)
    {
        if (!IsOpen) return;
        if (IsThemeStep)
        {
            var all = AllThemes();
            if (index >= 0 && index < all.Count) SelectTheme(all[index]);
        }
        else if (index >= 0 && index < _options.Count) Select(_options[index]);
    }

    public void Move(int delta)
    {
        if (!IsOpen) return;
        if (IsThemeStep)
        {
            var all = AllThemes();
            if (all.Count == 0) return;
            SelectIndex(Math.Clamp(ThemeIndex(all) + delta, 0, all.Count - 1));
            return;
        }
        if (_options.Count == 0) return;
        int at = _selected is null ? 0 : IndexOf(_selected);
        SelectIndex(Math.Clamp(at + delta, 0, _options.Count - 1));
    }

    public void MoveRows(int delta)
    {
        if (!IsOpen || !IsThemeStep || _selectedTheme is null) return;
        var all = AllThemes();
        int at = ThemeIndex(all);
        int nb = _builtIn.Count;
        int target = at;
        if (at < nb)
        {
            // In the built-in group: step a row inside it, or cross into the user group's first/last row.
            if (delta > 0)
            {
                if (at / Columns < (nb - 1) / Columns) target = Math.Min(at + Columns, nb - 1);
                else if (_user.Count > 0) target = nb + Math.Min(at % Columns, _user.Count - 1);
            }
            else if (at >= Columns) target = at - Columns;
        }
        else
        {
            int j = at - nb;
            if (delta > 0)
            {
                if (j / Columns < (_user.Count - 1) / Columns) target = nb + Math.Min(j + Columns, _user.Count - 1);
            }
            else if (j >= Columns) target = at - Columns;
            else if (nb > 0) target = Math.Min((nb - 1) / Columns * Columns + j % Columns, nb - 1);
        }
        if (target != at) SelectIndex(target);
    }

    public void MoveToEdge(bool end)
    {
        if (!IsOpen) return;
        int count = IsThemeStep ? AllThemes().Count : _options.Count;
        if (count > 0) SelectIndex(end ? count - 1 : 0);
    }

    public void Next()
    {
        if (!IsOpen || !IsThemeStep) return;
        Step = LayoutWizardStep.Waveform;
    }

    public void Back()
    {
        if (!IsOpen || !IsWaveformStep) return;
        Step = LayoutWizardStep.Theme;
    }

    public void Apply()
    {
        if (!IsOpen) return;
        if (_selected is { } s) _waveformStyle.Choose(s.Strategy);
        if (_selectedTheme is { } t) _themes.Choose(t.Theme);
        Close();
    }

    public void Cancel()
    {
        if (!IsOpen) return;
        _waveformStyle.Preview(_waveformStyle.Chosen);
        _themes.Preview(_themes.Chosen);
        Close();
    }

    private IReadOnlyList<ThemeOption> AllThemes() => [.. _builtIn, .. _user];

    private int ThemeIndex(IReadOnlyList<ThemeOption> all)
    {
        for (int i = 0; i < all.Count; i++)
            if (ReferenceEquals(all[i], _selectedTheme)) return i;
        return 0;
    }

    private void Close()
    {
        _generation++;
        _scroll.Stop();
        IsOpen = false;
    }

    private int IndexOf(WaveformStyleOption option)
    {
        for (int i = 0; i < _options.Count; i++)
            if (ReferenceEquals(_options[i], option)) return i;
        return 0;
    }

    private void SetSelected(WaveformStyleOption? option)
    {
        if (ReferenceEquals(_selected, option)) return;
        if (_selected is not null) _selected.IsSelected = false;
        _selected = option;
        if (option is not null) option.IsSelected = true;
        Notify(nameof(Selected));
    }

    private void SetSelectedTheme(ThemeOption? option)
    {
        if (ReferenceEquals(_selectedTheme, option)) return;
        if (_selectedTheme is not null) _selectedTheme.IsSelected = false;
        _selectedTheme = option;
        if (option is not null) option.IsSelected = true;
        Notify(nameof(SelectedTheme));
        Notify(nameof(ThemeSummary));
    }

    private void Notify(string name) => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
}
