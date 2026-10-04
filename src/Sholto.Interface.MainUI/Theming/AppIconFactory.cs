using System.IO;
using Avalonia.Controls;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using SkiaSharp;

namespace Sholto.Interface.MainUI.Theming;

/// <summary>Rasterise the Sholto brand mark: a rounded-square plate with three offset
/// "S" glyphs (blue/green/red, screen-blended) — the same RGB-split S as the Media
/// Library watermark.</summary>
public sealed class AppIconFactory : IAppIconFactory
{
    public WindowIcon Create(SholtoTheme theme)
    {
        const int size = 256;
        using var surface = SKSurface.Create(new SKImageInfo(size, size, SKColorType.Bgra8888, SKAlphaType.Premul));
        var canvas = surface.Canvas;
        canvas.Clear(SKColors.Transparent);

        // Rounded-square plate (Tokyo Night surface).
        using (var plate = new SKPaint { Color = ToSk(theme.IconPlate), IsAntialias = true })
            canvas.DrawRoundRect(new SKRect(0, 0, size, size), 56, 56, plate);

        using var tf = SKTypeface.FromFamilyName("Inter",
                           SKFontStyleWeight.ExtraBold, SKFontStyleWidth.Normal, SKFontStyleSlant.Upright)
                       ?? SKTypeface.FromFamilyName("Arial",
                           SKFontStyleWeight.ExtraBold, SKFontStyleWidth.Normal, SKFontStyleSlant.Upright)
                       ?? SKTypeface.Default;
        // Measure the glyph's TIGHT bounds (SKTextBlob.Bounds is a loose, inflated
        // box that threw the centring way off), then draw with TextAlign.Center so
        // the horizontal centre is just cx and only the vertical offset needs the
        // bounds.
        const float textSize = 220;
        var bounds = new SKRect();
        using (var probe = new SKPaint { Typeface = tf, TextSize = textSize, IsAntialias = true })
            probe.MeasureText("S", ref bounds);
        float cx = size / 2f;
        float baseY = size / 2f - bounds.MidY;   // bounds are baseline-relative → centre vertically

        void DrawS(float dx, float dy, SKColor c)
        {
            using var p = new SKPaint
            {
                Typeface = tf, TextSize = textSize, IsAntialias = true,
                TextAlign = SKTextAlign.Center, Color = c, BlendMode = SKBlendMode.Screen,
            };
            canvas.DrawText("S", cx + dx, baseY + dy, p);
        }
        // Offsets mirror sholto-icon.svg: blue back (up-left), green anchor, red front (down-right).
        DrawS(-12, 8, ToSk(theme.Stems.Drums));   // drums – blue
        DrawS(0, 0, ToSk(theme.Stems.Vocals));     // vocals – green
        DrawS(12, -8, ToSk(theme.Stems.Instrumental));   // instrumental – red

        using var img = surface.Snapshot();
        using var data = img.Encode(SKEncodedImageFormat.Png, 100);
        var ms = new MemoryStream();
        data.SaveTo(ms);
        ms.Position = 0;
        return new WindowIcon(new Bitmap(ms));
    }

    private SKColor ToSk(Color c) => new SKColor(c.R, c.G, c.B, c.A);
}
