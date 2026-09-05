using Avalonia.Media;

namespace Sholto.App.Theming;

/// <summary>Named colour presets a theme can reference by <c>"waveformPalette"</c>.
/// Historically each preset carried its own three band colours, but the app
/// has drawn the Rekordbox 3-band scheme for every theme for a while now
/// (it reads far better for spotting the kick). So a preset only contributes
/// its downbeat-guide colour by default; its band colours are still here for a
/// theme that wants them via explicit <c>low</c>/<c>mid</c>/<c>high</c> keys.</summary>
public enum WaveformPreset { Bands, Hot, Plasma, Smoke, Glacier, OctoberRust, Massacre, Soule, Pantera }

public static class WaveformPresets
{
    /// <summary>Denon/Rekordbox 3-band: low blue, mid orange, high white.</summary>
    public static readonly (Color Low, Color Mid, Color High) ThreeBand =
        (Color.Parse("#2A7FFF"), Color.Parse("#FF8C1A"), Color.Parse("#F5F5FF"));

    public static (Color Low, Color Mid, Color High) Bands(WaveformPreset p) => p switch
    {
        WaveformPreset.Hot            => (Color.Parse("#FF3D3D"), Color.Parse("#3DFF7A"), Color.Parse("#3D8BFF")),
        WaveformPreset.Plasma         => (Color.Parse("#7C5CFF"), Color.Parse("#FF4E9A"), Color.Parse("#34F0C6")),
        WaveformPreset.Smoke          => (Color.Parse("#5A4636"), Color.Parse("#E0A860"), Color.Parse("#F2E9D0")),
        WaveformPreset.Glacier        => (Color.Parse("#4C6B8A"), Color.Parse("#ECF0F6"), Color.Parse("#B48EAD")),
        WaveformPreset.OctoberRust    => (Color.Parse("#2D5512"), Color.Parse("#69BE28"), Color.Parse("#DCE6CF")),
        WaveformPreset.Massacre       => (Color.Parse("#5B4AE0"), Color.Parse("#D45CE0"), Color.Parse("#F2DEFF")),
        WaveformPreset.Soule          => (Color.Parse("#2E4734"), Color.Parse("#6A8F62"), Color.Parse("#E8EDE5")),
        WaveformPreset.Pantera        => (Color.Parse("#7A3D22"), Color.Parse("#FF6B2C"), Color.Parse("#E0D8CC")),
        _                             => ThreeBand,
    };

    /// <summary>Downbeat-guide colour chosen to contrast each preset's high band.</summary>
    public static Color Downbeat(WaveformPreset p) => p switch
    {
        WaveformPreset.Hot            => Color.FromArgb(0xC8, 0xFF, 0xD6, 0x3D),
        WaveformPreset.Plasma         => Color.FromArgb(0xC8, 0xFF, 0xAA, 0x2A),
        WaveformPreset.Smoke          => Color.FromArgb(0xD0, 0xF2, 0xC8, 0x79),
        WaveformPreset.Glacier        => Color.FromArgb(0xD0, 0xA3, 0xBE, 0x8C),
        WaveformPreset.OctoberRust    => Color.FromArgb(0xD8, 0xD8, 0xA2, 0x4F),
        WaveformPreset.Massacre       => Color.FromArgb(0xD8, 0xFF, 0xFA, 0xF5),
        WaveformPreset.Soule          => Color.FromArgb(0xD8, 0xD4, 0xB8, 0x6A),
        WaveformPreset.Pantera        => Color.FromArgb(0xD8, 0xC5, 0xBF, 0xB5),
        _                             => Color.FromArgb(0xD8, 0xE6, 0xF0, 0xFF),
    };
}

/// <summary>
/// Every colour <see cref="Sholto.App.Controls.WaveformControl"/> draws. Comes from
/// an optional "waveform" JSON section (<see cref="SholtoThemeJson"/>); any key —
/// or the whole section — left out is filled by <see cref="DeriveFrom"/> so no
/// theme needs editing to stay valid. Alpha is part of the colour: the defaults
/// carry the same alphas the control used to hard-code.
/// The innermost (High) band is fixed white on every theme so the vocal-presence
/// overlay stays readable.
/// </summary>
public sealed record WaveformPalette(
    Color Background,   // baked image background
    Color Low,          // bass band
    Color Mid,
    Color Downbeat,     // bar guide line
    Color BeatTick,     // per-beat tick
    Color Playhead,
    Color Marker,       // user markers / cue pins
    Color Gain,         // channel/crossfader gain line
    Color Loop)         // active loop band (translucent)
{
    private static Color WithAlpha(Color c, byte a) => Color.FromArgb(a, c.R, c.G, c.B);

    public static WaveformPalette DeriveFrom(WaveformPreset preset, Color bgDeep, Color accent,
        Color mint, Color textBright, Color textMuted)
    {
        var (lo, mid, _) = WaveformPresets.ThreeBand;
        return new WaveformPalette(
            Background:  Color.Parse("#111111"),
            Low:         lo,
            Mid:         mid,
            Downbeat:    WaveformPresets.Downbeat(preset),
            BeatTick:    WithAlpha(textBright, 0xC0),
            Playhead:    WithAlpha(mint, 0xFF),
            Marker:      WithAlpha(accent, 0xFF),
            Gain:        WithAlpha(mint, 0xFF),
            Loop:        WithAlpha(accent, 0x80));
    }
}
