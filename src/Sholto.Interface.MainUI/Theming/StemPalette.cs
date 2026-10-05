using Avalonia.Media;

namespace Sholto.Interface.MainUI.Theming;

/// <summary>The three stem colours (drums, vocals, instrumental) shared by the stem buttons and the window icon, plus the label colour drawn on a lit stem chip.</summary>
public sealed record StemPalette(Color Drums, Color Vocals, Color Instrumental, Color ChipText);
