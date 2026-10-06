using Sholto.Interface.MainUI.Controls.Modal;
using Sholto.Interface.MainUI.Theming;

namespace Sholto.Interface.MainUI.ViewModels;

/// <summary>The Layout Wizard overlay (Settings ▸ Layout Wizard…). Two steps: the theme, then the waveform
/// style. Picking a card previews it live (the decks redraw; the whole app retones); nothing is saved until
/// Apply, which commits both choices. Cancel / Esc puts back the style and theme in use when the wizard opened.
/// Presentation state only: the App sees nothing until Apply sends <c>ChooseWaveformStyle</c> and
/// <c>ChooseTheme</c>.
/// As a modal the wizard is multi-step content: <see cref="IModalContent.Eyebrow"/>, <c>Title</c>, <c>Subtitle</c>,
/// <c>KeyHint</c>, <c>Buttons</c> and <c>CanGoBack</c> change with the step, and <see cref="IModalContent.Confirm"/>
/// is <see cref="Next"/> on the theme step and <see cref="Apply"/> on the waveform step; <c>Dismiss</c> is
/// <see cref="Cancel"/>.</summary>
public interface ILayoutWizardViewModel : IModalContent
{
    /// <summary>The step showing.</summary>
    LayoutWizardStep Step { get; }
    bool IsWaveformStep { get; }
    bool IsThemeStep { get; }

    /// <summary>Name of the theme picked on step 1 (shown on the done step in the stepper).</summary>
    string ThemeSummary { get; }

    /// <summary>The waveform step's cards, in the order of the style catalogue.</summary>
    IReadOnlyList<WaveformStyleOption> Options { get; }

    WaveformStyleOption? Selected { get; }

    /// <summary>The theme step's bundled-theme cards, in manifest order.</summary>
    IReadOnlyList<ThemeOption> BuiltInThemes { get; }

    /// <summary>The theme step's cards for themes in the user themes folder.</summary>
    IReadOnlyList<ThemeOption> UserThemes { get; }

    bool HasUserThemes { get; }

    /// <summary>"BUILT-IN · n".</summary>
    string BuiltInHeading { get; }

    /// <summary>"YOUR THEMES · n".</summary>
    string UserHeading { get; }

    /// <summary>The user themes folder, for the empty-state tile.</summary>
    string UserThemesDirectory { get; }

    ThemeOption? SelectedTheme { get; }

    /// <summary>Open on the chosen theme and style, at the theme step. The preview cards play a faked demo track
    /// (never a deck's), drawn in <paramref name="palette"/>, the current theme's.</summary>
    void Open(WaveformPalette palette);

    /// <summary>Where the preview cards are in the demo track; the cards scroll to it.</summary>
    IWaveformPreviewScroll Scroll { get; }

    /// <summary>Select a waveform card and preview its style on the decks.</summary>
    void Select(WaveformStyleOption option);

    /// <summary>Select a theme card and try that theme on the whole app.</summary>
    void SelectTheme(ThemeOption option);

    /// <summary>Select by 0-based index on the step showing (number keys on the waveform step); out of range is ignored.</summary>
    void SelectIndex(int index);

    /// <summary>Move the selection left (-1) or right (+1) on the step showing, clamped.</summary>
    void Move(int delta);

    /// <summary>Move the theme selection up (-1) or down (+1) a row of the four-column grid. Theme step only.</summary>
    void MoveRows(int delta);

    /// <summary>Select the first (or, with <paramref name="end"/>, last) card of the step showing.</summary>
    void MoveToEdge(bool end);

    /// <summary>Go to the waveform step. Theme step only.</summary>
    void Next();

    // Back() is IModalContent's: back to the theme step, waveform step only.

    /// <summary>Choose the picked waveform style and theme (both saved) and close.</summary>
    void Apply();

    /// <summary>Revert to the style and theme in use when the wizard opened and close.</summary>
    void Cancel();
}
