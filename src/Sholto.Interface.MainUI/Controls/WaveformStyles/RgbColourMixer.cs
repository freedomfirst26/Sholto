using SkiaSharp;

namespace Sholto.Interface.MainUI.Controls.WaveformStyles;

/// <summary>Default <see cref="IRgbColourMixer"/>. Mostly a band's LEAD (<c>floorShare</c> of the floor is
/// removed: 1 is pure lead-takes-all, 0 the plain weights, in between a blend that still tints toward the
/// leading band). A band's LEAD colours a column: bass and treble
/// each show their excess over the weakest band, and mids show only their excess over the stronger of
/// the other two. In a loud section every band sits near its own track reference, so the plain weights
/// are all about 1 and a linear mix is a pale teal-white everywhere; the leads are what differ between
/// a kick column and a hat column. Mids are treated as the bed of every mix (snare body, synths, vocals
/// run under kicks and hats alike), so green appears only where mids genuinely dominate, while bass and
/// treble mix freely — kick + hat is the pink/magenta that Rekordbox's RGB is known for.
///
/// The leads are squared (<see cref="Sharpness"/>) so the dominant band wins decisively, summed over
/// the band colours per channel, and scaled so the largest channel is 255 (brightness is the style's
/// business, not the mixer's). A column with no lead at all (every band equal) falls back to the even mix
/// of the three colours, which is also what the no-band-data bake uses.</summary>
public sealed class RgbColourMixer(float floorShare) : IRgbColourMixer
{
    private readonly float _floorShare = floorShare;

    /// <summary>Exponent applied to each lead before mixing; 2 lets the dominant band win without turning
    /// every column into a pure primary.</summary>
    private const float Sharpness = 2f;

    public SKColor Mix(float low, float mid, float high, SKColor lowColour, SKColor midColour, SKColor highColour)
    {
        low = MathF.Max(0f, low);
        mid = MathF.Max(0f, mid);
        high = MathF.Max(0f, high);
        float floor = _floorShare * MathF.Min(low, MathF.Min(mid, high));
        float l = low - floor;
        float h = high - floor;
        float m = MathF.Max(0f, mid - _floorShare * MathF.Max(low, high));
        if (l <= 0f && m <= 0f && h <= 0f) { l = low; m = mid; h = high; }

        l = Sharpen(l);
        m = Sharpen(m);
        h = Sharpen(h);
        float r = l * lowColour.Red   + m * midColour.Red   + h * highColour.Red;
        float g = l * lowColour.Green + m * midColour.Green + h * highColour.Green;
        float b = l * lowColour.Blue  + m * midColour.Blue  + h * highColour.Blue;
        float peak = MathF.Max(r, MathF.Max(g, b));
        if (peak <= 0f) return SKColors.Transparent;
        float k = 255f / peak;
        return new SKColor(
            (byte)MathF.Round(r * k),
            (byte)MathF.Round(g * k),
            (byte)MathF.Round(b * k),
            0xFF);
    }

    private float Sharpen(float w) => w <= 0f ? 0f : MathF.Pow(w, Sharpness);
}
