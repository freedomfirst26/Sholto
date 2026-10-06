namespace Sholto.Interface.MainUI.Theming;

/// <summary>Creates a <see cref="SholtoTheme"/> from theme JSON.</summary>
public interface ISholtoThemeFactory
{
    /// <summary>Creates a theme from its JSON text; throws on malformed or missing required keys.</summary>
    SholtoTheme Create(string json);
}
