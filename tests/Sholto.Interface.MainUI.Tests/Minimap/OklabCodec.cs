using Avalonia.Media;

namespace Sholto.Interface.MainUI.Tests.Minimap;

/// <summary>sRGB to and from OKLab, with Björn Ottosson's reference matrices.</summary>
public sealed class OklabCodec
{
    public (double L, double A, double B) Encode(Color c)
    {
        double r = ToLinear(c.R / 255.0), g = ToLinear(c.G / 255.0), b = ToLinear(c.B / 255.0);
        double l = Math.Cbrt(0.4122214708 * r + 0.5363325363 * g + 0.0514459929 * b);
        double m = Math.Cbrt(0.2119034982 * r + 0.6806995451 * g + 0.1073969566 * b);
        double s = Math.Cbrt(0.0883024619 * r + 0.2817188376 * g + 0.6299787005 * b);
        return (0.2104542553 * l + 0.7936177850 * m - 0.0040720468 * s,
                1.9779984951 * l - 2.4285922050 * m + 0.4505937099 * s,
                0.0259040371 * l + 0.7827717662 * m - 0.8086757660 * s);
    }

    public Color Decode(double L, double A, double B)
    {
        double l = Math.Pow(L + 0.3963377774 * A + 0.2158037573 * B, 3);
        double m = Math.Pow(L - 0.1055613458 * A - 0.0638541728 * B, 3);
        double s = Math.Pow(L - 0.0894841775 * A - 1.2914855480 * B, 3);
        double r = 4.0767416621 * l - 3.3077115913 * m + 0.2309699292 * s;
        double g = -1.2684380046 * l + 2.6097574011 * m - 0.3413193965 * s;
        double b = -0.0041960863 * l - 0.7034186147 * m + 1.7076147010 * s;
        return Color.FromRgb(ToByte(FromLinear(r)), ToByte(FromLinear(g)), ToByte(FromLinear(b)));
    }

    public double Distance((double L, double A, double B) x, (double L, double A, double B) y) =>
        Math.Sqrt(Math.Pow(x.L - y.L, 2) + Math.Pow(x.A - y.A, 2) + Math.Pow(x.B - y.B, 2));

    public double Chroma((double L, double A, double B) x) => Math.Sqrt(x.A * x.A + x.B * x.B);

    public double HueDegrees((double L, double A, double B) x) =>
        (Math.Atan2(x.B, x.A) * 180.0 / Math.PI + 360.0) % 360.0;

    private double ToLinear(double v) => v <= 0.04045 ? v / 12.92 : Math.Pow((v + 0.055) / 1.055, 2.4);

    private double FromLinear(double v) => v <= 0.0031308 ? 12.92 * v : 1.055 * Math.Pow(v, 1 / 2.4) - 0.055;

    private byte ToByte(double v) => (byte)Math.Round(Math.Clamp(v, 0, 1) * 255);
}
