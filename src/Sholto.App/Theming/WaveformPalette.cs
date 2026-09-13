using Avalonia.Media;

namespace Sholto.App.Theming;

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
