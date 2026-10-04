using Avalonia.Media;

namespace Sholto.Interface.MainUI.Theming;

/// <summary>Fills a <see cref="WaveformPalette"/> from a theme's core colours.</summary>
public interface IWaveformPaletteFactory
{
    WaveformPalette FromTheme(WaveformPreset preset, Color bgDeep, Color accent,
        Color mint, Color textBright, Color textMuted);
}
