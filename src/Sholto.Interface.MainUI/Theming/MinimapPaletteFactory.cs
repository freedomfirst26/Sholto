using Avalonia.Media;

namespace Sholto.Interface.MainUI.Theming;

/// <summary>Computes a full <see cref="MinimapPalette"/> from a theme's already-parsed
/// core colours, used whenever a theme's JSON omits the "minimap" section entirely,
/// or omits individual keys within it.</summary>
public sealed class MinimapPaletteFactory : IMinimapPaletteFactory
{
    /// <summary>Blend <paramref name="a"/> toward <paramref name="b"/> by
    /// <paramref name="t"/> (0 = a, 1 = b), channel-wise, alpha held at 0xFF.</summary>
    private Color Blend(Color a, Color b, double t) => Color.FromRgb(
        (byte)(a.R + (b.R - a.R) * t),
        (byte)(a.G + (b.G - a.G) * t),
        (byte)(a.B + (b.B - a.B) * t));

    private Color WithAlpha(Color c, byte a) => Color.FromArgb(a, c.R, c.G, c.B);

    public MinimapPalette FromTheme(Color bgDeep, Color primary, Color accent, Color mint,
        Color textBright, Color border)
    {
        var backdrop  = bgDeep;
        var playhead  = mint;
        var label     = textBright;
        var divider   = WithAlpha(bgDeep, 0xB0);
        var drop      = accent;
        var chorus    = Blend(accent, mint, 0.30);
        var buildUp   = primary;
        var verse     = Blend(primary, border, 0.50);
        var breakdown = Blend(accent, primary, 0.50);
        var bridge    = mint;
        var intro     = border;
        var outro     = Blend(border, bgDeep, 0.50);

        return new MinimapPalette(backdrop, playhead, label, divider,
            intro, buildUp, drop, breakdown, verse, chorus, bridge, outro);
    }
}
