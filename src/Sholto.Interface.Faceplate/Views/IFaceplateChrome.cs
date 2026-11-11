using Avalonia;
using Avalonia.Controls;

namespace Sholto.Interface.Faceplate.Views;

/// <summary>Paints the guide's scrim, feather, panel face and cast shadow from the current theme.</summary>
public interface IFaceplateChrome
{
    /// <summary>Repaints the backdrop, feather and panel from the theme as <paramref name="host"/> sees it.</summary>
    void Paint(Visual host, Panel backdrop, Border feather, Border panel);
}
