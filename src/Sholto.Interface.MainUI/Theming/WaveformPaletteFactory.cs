using Avalonia.Media;

namespace Sholto.Interface.MainUI.Theming;

/// <summary>Computes a full <see cref="WaveformPalette"/> from a theme's already-parsed
/// core colours; used for any key (or the whole "waveform" section) a theme's JSON omits.
/// Non-derivable colours come from <see cref="IThemeDefaults"/> (<c>waveform.*</c>).</summary>
public sealed class WaveformPaletteFactory(IWaveformPresets presets, IThemeDefaults defaults) : IWaveformPaletteFactory
{
    private readonly IWaveformPresets _presets = presets;
    private readonly IThemeDefaults _defaults = defaults;

    private Color WithAlpha(Color c, byte a) => Color.FromArgb(a, c.R, c.G, c.B);

    /// <summary>A missing defaults.json entry shows as magenta rather than a silent literal.</summary>
    private Color Default(string key) => _defaults.TryGetColor("waveform." + key, out var c) ? c : Colors.Magenta;

    public WaveformPalette FromTheme(WaveformPreset preset, Color bgDeep, Color accent,
        Color mint, Color textBright, Color textMuted)
    {
        var (lo, mid, _) = _presets.ThreeBand;
        return new WaveformPalette(
            Background:    Default("background"),
            Low:           lo,
            Mid:           mid,
            Downbeat:      _presets.Downbeat(preset),
            BeatTick:      WithAlpha(textBright, 0xC0),
            Playhead:      WithAlpha(mint, 0xFF),
            Marker:        WithAlpha(accent, 0xFF),
            Gain:          WithAlpha(mint, 0xFF),
            Loop:          WithAlpha(accent, 0x80),
            High:          Default("high"),
            Vocal:         Default("vocal"),
            VocalInactive: Default("vocalInactive"),
            SnapGlow:      Default("snapGlow"),
            GridEdit:      Default("gridEdit"),
            Edge:          Default("edge"));
    }
}
