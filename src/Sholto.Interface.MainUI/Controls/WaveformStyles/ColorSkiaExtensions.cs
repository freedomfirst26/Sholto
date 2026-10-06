using SkiaSharp;

namespace Sholto.Interface.MainUI.Controls.WaveformStyles;

/// <summary>Avalonia theme colour → Skia colour for the bake.</summary>
public static class ColorSkiaExtensions
{
    public static SKColor ToSk(this Avalonia.Media.Color c) => new(c.R, c.G, c.B, c.A);
}
