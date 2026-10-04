namespace Sholto.Interface.MainUI.Theming;

/// <summary>The themes available to the app, in menu order.</summary>
public interface IThemeCatalog
{
    IReadOnlyList<SholtoTheme> All { get; }

    /// <summary>The theme with this name (case-insensitive). If it is missing — for
    /// instance a user deleted its JSON — falls back to the first available theme so
    /// the UI never crashes; throws only if no theme loaded at all.</summary>
    SholtoTheme ByName(string name);
}
