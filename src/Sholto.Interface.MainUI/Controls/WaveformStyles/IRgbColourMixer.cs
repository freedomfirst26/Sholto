using SkiaSharp;

namespace Sholto.Interface.MainUI.Controls.WaveformStyles;

/// <summary>The RGB style's colour for one column: the three band colours weighted by how strongly
/// each band is present, sharpened so the dominant band wins, and normalised to full brightness so the
/// colour shows the band MIX (hue) and the stroke height alone shows loudness.</summary>
public interface IRgbColourMixer
{
    /// <summary><paramref name="low"/>/<paramref name="mid"/>/<paramref name="high"/> are per-band
    /// levels relative to each band's typical level in the track (≥ 0, not clamped; the same proportions
    /// at any level give the same colour). Returns a fully opaque colour whose brightest channel is 255,
    /// or transparent when every weight is zero.</summary>
    SKColor Mix(float low, float mid, float high, SKColor lowColour, SKColor midColour, SKColor highColour);
}
