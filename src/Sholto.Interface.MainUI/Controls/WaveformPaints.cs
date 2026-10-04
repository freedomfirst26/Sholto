using SkiaSharp;

namespace Sholto.Interface.MainUI.Controls;

/// <summary>
/// Render-thread paint cache owned by one <see cref="WaveformControl"/>. Paints are
/// created lazily, once per thread, then re-used and mutated rather than allocated
/// per frame — at 60 Hz x 2 decks a per-frame <c>new SKPaint</c> was a real GC source.
/// Per-thread (not per-operation) because we have not shown that Avalonia never
/// renders two operations for one control at once.
/// </summary>
public sealed class WaveformPaints
{
    private readonly ThreadLocal<SKPaint> _blit = new(() => new SKPaint { FilterQuality = SKFilterQuality.Low });
    private readonly ThreadLocal<SKPaint> _head = new(() => new SKPaint { StrokeWidth = 2, IsAntialias = false });
    private readonly ThreadLocal<SKPaint> _gain = new(() => new SKPaint { StrokeWidth = 1, IsAntialias = false });
    private readonly ThreadLocal<SKPaint> _db = new(() => new SKPaint { StrokeWidth = 2, IsAntialias = false });
    private readonly ThreadLocal<SKPaint> _glow = new(() => new SKPaint { IsAntialias = false });
    private readonly ThreadLocal<SKPaint> _beatTick = new(() => new SKPaint { StrokeWidth = 1, IsAntialias = false });
    private readonly ThreadLocal<SKPaint> _loop = new(() => new SKPaint { Style = SKPaintStyle.Fill, IsAntialias = false });
    private readonly ThreadLocal<SKPaint> _vocal = new(() => new SKPaint { Style = SKPaintStyle.Fill, IsAntialias = false });
    private readonly ThreadLocal<SKPaint> _marker = new(() => new SKPaint { IsAntialias = false });

    public SKPaint Blit => _blit.Value!;
    public SKPaint Head => _head.Value!;
    public SKPaint Gain => _gain.Value!;
    public SKPaint Db => _db.Value!;
    public SKPaint Glow => _glow.Value!;
    public SKPaint BeatTick => _beatTick.Value!;
    public SKPaint Loop => _loop.Value!;
    public SKPaint Vocal => _vocal.Value!;
    public SKPaint Marker => _marker.Value!;
}
