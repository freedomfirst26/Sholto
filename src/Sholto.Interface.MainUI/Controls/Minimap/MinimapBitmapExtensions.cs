using Avalonia;
using Avalonia.Media.Imaging;
using Avalonia.Platform;
using SkiaSharp;

namespace Sholto.Interface.MainUI.Controls.Minimap;

public static class MinimapBitmapExtensions
{
    /// <summary>Copy a baked image into a bitmap Avalonia can draw, pixel for pixel (96 dpi, so one image
    /// pixel is one device pixel when drawn 1:1).</summary>
    public static WriteableBitmap ToBitmap(this SKImage image)
    {
        var bitmap = new WriteableBitmap(new PixelSize(image.Width, image.Height), new Vector(96, 96), PixelFormat.Bgra8888, AlphaFormat.Premul);
        using var frame = bitmap.Lock();
        var info = new SKImageInfo(image.Width, image.Height, SKColorType.Bgra8888, SKAlphaType.Premul);
        image.ReadPixels(info, frame.Address, frame.RowBytes, 0, 0);
        return bitmap;
    }
}
