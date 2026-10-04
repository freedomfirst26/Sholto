using Avalonia.Controls;

namespace Sholto.Interface.MainUI.Theming;

/// <summary>Draws the Sholto brand mark as an icon: the window / taskbar / alt-tab icon
/// and the system tray icon are the same image.</summary>
public interface IAppIconFactory
{
    /// <summary>A rounded plate in the theme's <c>IconPlate</c> colour with three offset
    /// "S" glyphs in the theme's stem colours (screen-blended).</summary>
    WindowIcon Create(SholtoTheme theme);
}
