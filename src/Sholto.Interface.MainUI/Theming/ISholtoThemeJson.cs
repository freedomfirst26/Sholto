namespace Sholto.Interface.MainUI.Theming;

/// <summary>Reads every bundled and user theme from JSON.</summary>
public interface ISholtoThemeJson
{
    /// <summary>All bundled themes (in manifest order) followed by user themes whose
    /// names do not collide with a bundled one. Needs a live Avalonia application.</summary>
    IReadOnlyList<SholtoTheme> LoadAll();
}
