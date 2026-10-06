using System.Security.Cryptography;
using SkiaSharp;

namespace Sholto.Interface.MainUI.Tests.WaveformStyles;

/// <summary>Reads a baked waveform image back for assertions.</summary>
internal sealed class BakedPixels
{
    public string Sha256(SKImage image)
    {
        using var bmp = SKBitmap.FromImage(image);
        return Convert.ToHexString(SHA256.HashData(bmp.Bytes));
    }

    /// <summary>True for every pixel that is not exactly <paramref name="background"/>: the drawn
    /// silhouette including its anti-aliased edge.</summary>
    public bool[] Coverage(SKImage image, SKColor background)
    {
        using var bmp = SKBitmap.FromImage(image);
        var mask = new bool[bmp.Width * bmp.Height];
        for (int y = 0; y < bmp.Height; y++)
            for (int x = 0; x < bmp.Width; x++)
                mask[y * bmp.Width + x] = bmp.GetPixel(x, y) != background;
        return mask;
    }

    public SKColor At(SKImage image, int x, int y)
    {
        using var bmp = SKBitmap.FromImage(image);
        return bmp.GetPixel(x, y);
    }
}
