using Sholto.Interface.MainUI.Theming;

namespace Sholto.Interface.MainUI.ViewModels;

/// <summary>Builds a wizard card for a theme.</summary>
public interface IThemeOptionFactory
{
    /// <param name="isCurrent">The theme in use when the wizard opened.</param>
    ThemeOption Create(SholtoTheme theme, bool isCurrent);
}
