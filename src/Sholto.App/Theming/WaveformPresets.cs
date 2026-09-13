using Avalonia.Media;

namespace Sholto.App.Theming;

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
