using System.ComponentModel;
using Sholto.Interface.MainUI.Theming;

namespace Sholto.Interface.MainUI.ViewModels;

/// <summary>Which theme the whole app wears. Presentation state, owned by MainUI; the App only remembers
/// the chosen name (<c>ChooseTheme</c> → settings, <c>SavedThemeFound</c> back on startup).
///
/// Two values, because the Layout Wizard tries themes on live: <see cref="Shown"/> is what the app wears
/// right now; <see cref="Chosen"/> is what the user committed to (and what is saved). Cancelling the wizard
/// previews <see cref="Chosen"/> again.</summary>
public interface IThemeViewModel : INotifyPropertyChanged
{
    /// <summary>Every theme on offer: the bundled ones in manifest order, then the user's.</summary>
    IReadOnlyList<SholtoTheme> All { get; }

    /// <summary>The folder user themes are read from.</summary>
    string UserThemesDirectory { get; }

    /// <summary>What the app wears now. Raises PropertyChanged when it changes.</summary>
    SholtoTheme Shown { get; }

    /// <summary>What the user committed to; the saved choice.</summary>
    SholtoTheme Chosen { get; }

    /// <summary>Wear a theme without choosing it (the wizard's live try-on / cancel). Nothing is saved.</summary>
    void Preview(SholtoTheme theme);

    /// <summary>The user's choice: wear it, and when it differs from <see cref="Chosen"/> tell the App so it
    /// is remembered.</summary>
    void Choose(SholtoTheme theme);

    /// <summary>Wear the theme saved last time, if the catalog still has one of that name. Not a new choice,
    /// so nothing is sent back to the App.</summary>
    void Restore(string savedName);
}
