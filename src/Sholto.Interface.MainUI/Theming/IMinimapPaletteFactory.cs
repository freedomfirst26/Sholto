using Avalonia.Media;

namespace Sholto.Interface.MainUI.Theming;

/// <summary>Fills a <see cref="MinimapPalette"/> from a theme's core colours.</summary>
public interface IMinimapPaletteFactory
{
    MinimapPalette FromTheme(Color bgDeep, Color primary, Color accent, Color mint,
        Color textBright, Color border);
}
