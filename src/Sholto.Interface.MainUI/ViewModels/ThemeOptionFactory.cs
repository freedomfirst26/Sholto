using Sholto.Data;
using Sholto.Interface.MainUI.Theming;

namespace Sholto.Interface.MainUI.ViewModels;

/// <summary>Default <see cref="IThemeOptionFactory"/>: the miniature's three key chips are 8B, 8A and 3A in
/// the theme's own Camelot palette.</summary>
public sealed class ThemeOptionFactory : IThemeOptionFactory
{
    public ThemeOption Create(SholtoTheme theme, bool isCurrent)
    {
        var camelot = theme.CamelotPalette;
        return new ThemeOption(theme, isCurrent,
        [
            camelot.KeyBrush(new KeyRef(0, true)),
            camelot.KeyBrush(new KeyRef(9, false)),
            camelot.KeyBrush(new KeyRef(5, false)),
        ]);
    }
}
