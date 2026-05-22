using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;
using Avalonia.Rendering.SceneGraph;
using Avalonia.Skia;
using Avalonia.Threading;
using Sholto.Audio;
using Sholto.Analysis;
using SkiaSharp;

namespace Sholto.App.Controls;

public enum WaveformPalette { Bands, Hot, Plasma, Smoke, Glacier, SubFocus, OctoberRust, Massacre, Soule, BoardsOfCanada, Pantera }

/// <summary>
/// Pre-renders the entire waveform to an offscreen SKImage at track load,
/// then blits a window per frame. One textured rectangle per frame instead of
/// thousands of DrawLine calls.
/// </summary>
public sealed class WaveformControl : Control
{
    private const int BakedHeight = 256;
    private static readonly SKColor BgColor = new(0x11, 0x11, 0x11);

    public static readonly StyledProperty<WaveformPeaks?> PeaksProperty =
        AvaloniaProperty.Register<WaveformControl, WaveformPeaks?>(nameof(Peaks));

    /// <summary>Mixed-track peaks used purely as the time→pixel reference for the
    /// scrolling beatgrid and live overlays. Stays populated even when every stem
    /// is muted, so the grid keeps moving while <see cref="Peaks"/> is empty.</summary>
    public static readonly StyledProperty<WaveformPeaks?> GridPeaksProperty =
        AvaloniaProperty.Register<WaveformControl, WaveformPeaks?>(nameof(GridPeaks));

    public static readonly StyledProperty<double[]?> BeatTimesProperty =
        AvaloniaProperty.Register<WaveformControl, double[]?>(nameof(BeatTimes));

    public static readonly StyledProperty<double[]?> DownbeatTimesProperty =
        AvaloniaProperty.Register<WaveformControl, double[]?>(nameof(DownbeatTimes));

    public static readonly StyledProperty<double> PlayPositionProperty =
        AvaloniaProperty.Register<WaveformControl, double>(nameof(PlayPosition));

    /// <summary>Live tempo multiplier from the deck (1.0 = unity). Higher = compressed
    /// waveform (more peaks per pixel); lower = stretched. Matches the visual feel of
    /// Serato/Rekordbox when the pitch fader moves.</summary>
    public static readonly StyledProperty<double> PlaybackSpeedProperty =
        AvaloniaProperty.Register<WaveformControl, double>(nameof(PlaybackSpeed), 1.0);

    /// <summary>Theme-driven colour for the effective-gain (volume × crossfader)
    /// horizontal line drawn on the waveform. Falls back to mint if unbound.</summary>
    public static readonly StyledProperty<Avalonia.Media.Color> GainOverlayColorProperty =
        AvaloniaProperty.Register<WaveformControl, Avalonia.Media.Color>(
            nameof(GainOverlayColor), Avalonia.Media.Color.FromArgb(0xC0, 0x34, 0xF0, 0xC6));

    public Avalonia.Media.Color GainOverlayColor
    {
        get => GetValue(GainOverlayColorProperty);
        set => SetValue(GainOverlayColorProperty, value);
    }

    public static readonly StyledProperty<double> GainOverlayProperty =
        AvaloniaProperty.Register<WaveformControl, double>(nameof(GainOverlay), 1.0);

    public static readonly StyledProperty<double> MagneticGlowSecProperty =
        AvaloniaProperty.Register<WaveformControl, double>(nameof(MagneticGlowSec), -1.0);

    public static readonly StyledProperty<bool> IsScrubbingProperty =
        AvaloniaProperty.Register<WaveformControl, bool>(nameof(IsScrubbing), false);

    public static readonly StyledProperty<WaveformPalette> PaletteProperty =
        AvaloniaProperty.Register<WaveformControl, WaveformPalette>(nameof(Palette), WaveformPalette.Bands);

    private SKImage? _baked;
    private WaveformPeaks? _bakedFor;
    private WaveformPalette _bakedPalette;
    private CancellationTokenSource? _bakeCts;

    static WaveformControl()
    {
        AffectsRender<WaveformControl>(PlayPositionProperty);
        AffectsRender<WaveformControl>(PlaybackSpeedProperty);
        AffectsRender<WaveformControl>(GainOverlayProperty);
        AffectsRender<WaveformControl>(GainOverlayColorProperty);
        AffectsRender<WaveformControl>(MagneticGlowSecProperty);
        AffectsRender<WaveformControl>(IsScrubbingProperty);
        PeaksProperty.Changed.AddClassHandler<WaveformControl>((c, _) => c.Rebake());
        PaletteProperty.Changed.AddClassHandler<WaveformControl>((c, _) => c.Rebake());
        // Beatgrid is drawn live (not baked), so it doesn't need a rebake — just
        // an invalidate so the next frame picks up the new ticks.
        AffectsRender<WaveformControl>(GridPeaksProperty);
        AffectsRender<WaveformControl>(BeatTimesProperty);
        AffectsRender<WaveformControl>(DownbeatTimesProperty);
    }

    public WaveformPeaks? Peaks
    {
        get => GetValue(PeaksProperty);
        set => SetValue(PeaksProperty, value);
    }

    public WaveformPeaks? GridPeaks
    {
        get => GetValue(GridPeaksProperty);
        set => SetValue(GridPeaksProperty, value);
    }

    public double[]? BeatTimes
    {
        get => GetValue(BeatTimesProperty);
        set => SetValue(BeatTimesProperty, value);
    }

    public double[]? DownbeatTimes
    {
        get => GetValue(DownbeatTimesProperty);
        set => SetValue(DownbeatTimesProperty, value);
    }

    public double PlayPosition
    {
        get => GetValue(PlayPositionProperty);
        set => SetValue(PlayPositionProperty, value);
    }

    public double PlaybackSpeed
    {
        get => GetValue(PlaybackSpeedProperty);
        set => SetValue(PlaybackSpeedProperty, value);
    }

    public double GainOverlay
    {
        get => GetValue(GainOverlayProperty);
        set => SetValue(GainOverlayProperty, value);
    }

    public double MagneticGlowSec
    {
        get => GetValue(MagneticGlowSecProperty);
        set => SetValue(MagneticGlowSecProperty, value);
    }

    /// <summary>When true and a magnetic beat is highlighted, draw a full-height green
    /// line instead of the top/bottom stripes — much easier to eyeball alignment while
    /// turning the jog wheel.</summary>
    public bool IsScrubbing
    {
        get => GetValue(IsScrubbingProperty);
        set => SetValue(IsScrubbingProperty, value);
    }

    public WaveformPalette Palette
    {
        get => GetValue(PaletteProperty);
        set => SetValue(PaletteProperty, value);
    }

    private void Rebake()
    {
        var peaks = Peaks;
        _bakeCts?.Cancel();
        if (peaks is null || peaks.Min.Length == 0)
        {
            _baked = null;
            _bakedFor = null;
            InvalidateVisual();
            return;
        }

        var cts = new CancellationTokenSource();
        _bakeCts = cts;
        var snapshot = peaks;
        var palette = Palette;
        Task.Run(() =>
        {
            var img = BakeWaveform(snapshot, palette, cts.Token);
            if (cts.IsCancellationRequested || img is null) { img?.Dispose(); return; }
            Dispatcher.UIThread.Post(() =>
            {
                if (cts.IsCancellationRequested) { img.Dispose(); return; }
                _baked = img;
                _bakedFor = snapshot;
                _bakedPalette = palette;
                InvalidateVisual();
            });
        });
    }

    private static SKImage? BakeWaveform(WaveformPeaks peaks, WaveformPalette palette, CancellationToken ct)
    {
        int width = peaks.Min.Length;
        if (width == 0) return null;
        int height = BakedHeight;
        float midY = height / 2f;

        var info = new SKImageInfo(width, height, SKColorType.Rgba8888, SKAlphaType.Premul);
        using var surface = SKSurface.Create(info);
        var canvas = surface.Canvas;
        canvas.Clear(BgColor);

        // Bands: authentic Rekordbox — blue / white / yellow
        // Hot:    Serato — red / green / blue
        // Plasma: Sholto 2026 — violet / hot-pink / mint
        // Smoke:  warm whiskey — coal / cream / amber
        // Glacier: nordic — slate-blue / frost-white / aurora-violet
        // SubFocus:    Nick Douwma's brand — deep crimson / signature bright red / pale rose
        // OctoberRust: Type O Negative album palette — deep forest / Pantone 369 C / bone
        // Massacre:    Birthday Massacre — deep crimson / hot magenta / pale pink (all-pink family)
        // Soule:       Jeremy Soule / Skyrim — pine green / forest moss / snow white
        // BoardsOfCanada: dreamy 80s VHS — deep ocean teal / faded blue / pale cassette cream
        // Pantera:     Cowboys From Hell — charcoal / flame orange / bone
        var (lowColor, midColor, highColor) = palette switch
        {
            WaveformPalette.Hot         => (new SKColor(0xFF, 0x3D, 0x3D), new SKColor(0x3D, 0xFF, 0x7A), new SKColor(0x3D, 0x8B, 0xFF)),
            WaveformPalette.Plasma      => (new SKColor(0x7C, 0x5C, 0xFF), new SKColor(0xFF, 0x4E, 0x9A), new SKColor(0x34, 0xF0, 0xC6)),
            WaveformPalette.Smoke       => (new SKColor(0x6B, 0x4F, 0x3A), new SKColor(0xE8, 0xD9, 0xB8), new SKColor(0xD4, 0xA5, 0x74)),
            WaveformPalette.Glacier     => (new SKColor(0x4C, 0x6B, 0x8A), new SKColor(0xEC, 0xF0, 0xF6), new SKColor(0xB4, 0x8E, 0xAD)),
            WaveformPalette.SubFocus    => (new SKColor(0x80, 0x14, 0x2E), new SKColor(0xFF, 0x1F, 0x3D), new SKColor(0xFF, 0xE5, 0xEA)),
            WaveformPalette.OctoberRust => (new SKColor(0x2D, 0x55, 0x12), new SKColor(0x69, 0xBE, 0x28), new SKColor(0xDC, 0xE6, 0xCF)),
            WaveformPalette.Massacre    => (new SKColor(0xB0, 0x24, 0x5C), new SKColor(0xFF, 0x3D, 0x9F), new SKColor(0xFF, 0xC2, 0xDA)),
            WaveformPalette.Soule       => (new SKColor(0x2E, 0x47, 0x34), new SKColor(0x6A, 0x8F, 0x62), new SKColor(0xE8, 0xED, 0xE5)),
            WaveformPalette.BoardsOfCanada => (new SKColor(0x2A, 0x44, 0x52), new SKColor(0x7F, 0xB6, 0xC9), new SKColor(0xDD, 0xF0, 0xF2)),
            WaveformPalette.Pantera     => (new SKColor(0x2A, 0x25, 0x20), new SKColor(0xFF, 0x6B, 0x2C), new SKColor(0xE0, 0xD8, 0xCC)),
            _                           => (new SKColor(0x1E, 0x59, 0xFF), new SKColor(0xFF, 0xFF, 0xFF), new SKColor(0xFF, 0xC7, 0x00)),
        };
        using var lowPaint  = new SKPaint { Color = lowColor, StrokeWidth = 1, IsAntialias = false };
        using var midPaint  = new SKPaint { Color = midColor, StrokeWidth = 1, IsAntialias = false };
        using var highPaint = new SKPaint { Color = highColor, StrokeWidth = 1, IsAntialias = false };

        bool hasBands = peaks.Low.Length == width;

        for (int x = 0; x < width; x++)
        {
            if (ct.IsCancellationRequested) return null;

            float amp = MathF.Max(MathF.Abs(peaks.Max[x]), MathF.Abs(peaks.Min[x]));
            float barHeight = amp * midY;
            if (barHeight < 1f) continue;

            if (!hasBands)
            {
                canvas.DrawLine(x, midY - barHeight, x, midY + barHeight, lowPaint);
                continue;
            }

            float l = peaks.Low[x];
            float m = peaks.Mid[x];
            float h = peaks.High[x];
            float sum = l + m + h;
            if (sum <= 1e-5f) continue;

            float hSeg = barHeight * (h / sum);
            float mSeg = barHeight * (m / sum);
            float lSeg = barHeight - hSeg - mSeg;

            float lowEdge  = lSeg;
            float midEdge  = lowEdge + mSeg;
            float highEdge = midEdge + hSeg;

            canvas.DrawLine(x, midY,           x, midY + lowEdge,  lowPaint);
            canvas.DrawLine(x, midY + lowEdge, x, midY + midEdge,  midPaint);
            canvas.DrawLine(x, midY + midEdge, x, midY + highEdge, highPaint);

            canvas.DrawLine(x, midY,           x, midY - lowEdge,  lowPaint);
            canvas.DrawLine(x, midY - lowEdge, x, midY - midEdge,  midPaint);
            canvas.DrawLine(x, midY - midEdge, x, midY - highEdge, highPaint);
        }

        // Beat ticks + downbeat grid are drawn live in BlitOperation so they
        // keep scrolling even when this baked body is empty (all stems muted).

        return surface.Snapshot();
    }

    public override void Render(DrawingContext context)
    {
        // Pick a downbeat-guide colour that contrasts the palette's high band so
        // the grid stays visible even where the waveform itself is yellow.
        SKColor downbeatColor = Palette switch
        {
            WaveformPalette.Hot       => new SKColor(0xFF, 0xD6, 0x3D, 0xC8), // yellow on red/green/blue
            WaveformPalette.Plasma    => new SKColor(0xFF, 0xAA, 0x2A, 0xC8), // amber on violet/pink/mint
            WaveformPalette.Smoke     => new SKColor(0xF2, 0xC8, 0x79, 0xD0), // candle gold on coal/cream/amber
            WaveformPalette.Glacier   => new SKColor(0xA3, 0xBE, 0x8C, 0xD0), // sage on slate/frost/violet
            WaveformPalette.SubFocus  => new SKColor(0xFF, 0xFF, 0xFF, 0xE0), // pure white on crimson/red/rose — Sub Focus's typography accent
            WaveformPalette.OctoberRust => new SKColor(0xD8, 0xA2, 0x4F, 0xD8), // rust amber on forest/lime/bone — picks up the album title's orange
            WaveformPalette.Massacre    => new SKColor(0xFF, 0xFA, 0xF5, 0xD8), // warm white on crimson/magenta/pale-pink — bone contrast against pink family
            WaveformPalette.Soule       => new SKColor(0xD4, 0xB8, 0x6A, 0xD8), // dragon gold on pine/moss/snow — Skyrim treasure-glint against the forest greens
            WaveformPalette.BoardsOfCanada => new SKColor(0xD4, 0xA8, 0x9A, 0xD0), // dusty mauve-peach on ocean/blue/cream — warm cassette-photo contrast against the cool blues
            WaveformPalette.Pantera     => new SKColor(0xC5, 0xBF, 0xB5, 0xD8), // gunmetal silver on charcoal/flame/bone — chrome contrast against the orange
            _                         => new SKColor(0xE6, 0xF0, 0xFF, 0xD8), // cool white on Rekordbox bands
        };
        // Volume/crossfader gain-line colour comes from the theme.
        var gainColor = new SKColor(GainOverlayColor.R, GainOverlayColor.G, GainOverlayColor.B, GainOverlayColor.A);
        context.Custom(new BlitOperation(new Rect(Bounds.Size), _baked, _bakedFor, GridPeaks, PlayPosition, PlaybackSpeed, GainOverlay, MagneticGlowSec, IsScrubbing, BeatTimes, DownbeatTimes, downbeatColor, gainColor));
    }

    private sealed class BlitOperation : ICustomDrawOperation
    {
        // Render-thread cached paints. Re-used and mutated rather than allocated
        // per frame — at 60 Hz × 2 decks the old per-frame `new SKPaint` pattern
        // was a real GC source on the render thread.
        [ThreadStatic] private static SKPaint? _blitPaint;
        [ThreadStatic] private static SKPaint? _headPaint;
        [ThreadStatic] private static SKPaint? _gainPaint;
        [ThreadStatic] private static SKPaint? _dbPaint;
        [ThreadStatic] private static SKPaint? _glowPaint;
        [ThreadStatic] private static SKPaint? _beatTickPaint;

        private readonly SKImage? _image;
        private readonly WaveformPeaks? _peaks;
        private readonly WaveformPeaks? _gridPeaks;
        private readonly double _playPosition;
        private readonly double _playbackSpeed;
        private readonly double _gain;
        private readonly double _magneticGlowSec;
        private readonly bool _isScrubbing;
        private readonly double[]? _beats;
        private readonly double[]? _downbeats;
        private readonly SKColor _downbeatColor;
        private readonly SKColor _gainColor;

        public BlitOperation(Rect bounds, SKImage? image, WaveformPeaks? peaks, WaveformPeaks? gridPeaks, double playPosition, double playbackSpeed, double gain, double magneticGlowSec, bool isScrubbing, double[]? beats, double[]? downbeats, SKColor downbeatColor, SKColor gainColor)
        {
            Bounds = bounds;
            _image = image;
            _peaks = peaks;
            _gridPeaks = gridPeaks;
            _playPosition = playPosition;
            // Guard: never let a runaway 0 collapse the window to zero width.
            _playbackSpeed = playbackSpeed > 0.01 ? playbackSpeed : 1.0;
            _gain = gain;
            _magneticGlowSec = magneticGlowSec;
            _isScrubbing = isScrubbing;
            _beats = beats;
            _downbeats = downbeats;
            _downbeatColor = downbeatColor;
            _gainColor = gainColor;
        }

        public Rect Bounds { get; }
        public bool HitTest(Point p) => Bounds.Contains(p);
        public bool Equals(ICustomDrawOperation? other) => false;
        public void Dispose() { }

        public void Render(ImmediateDrawingContext context)
        {
            var leaseFeature = (ISkiaSharpApiLeaseFeature?)context.TryGetFeature(typeof(ISkiaSharpApiLeaseFeature));
            if (leaseFeature is null) return;
            using var lease = leaseFeature.Lease();
            var canvas = lease.SkCanvas;

            int dstW = (int)Bounds.Width;
            int dstH = (int)Bounds.Height;
            canvas.Clear(BgColor);

            if (_image is not null && _peaks is not null && _peaks.Min.Length > 0)
            {
                int totalPeaks = _peaks.Min.Length;
                float centerPeak = (float)(_playPosition * totalPeaks); // keep sub-pixel precision
                // PlaybackSpeed > 1 → show MORE source peaks per pixel (compressed look).
                // PlaybackSpeed < 1 → show fewer (stretched). Mirrors how the beat grid is
                // drawn below so visuals stay locked together at any tempo.
                float half = (float)(dstW * _playbackSpeed / 2.0);

                float srcXStart = centerPeak - half;
                float srcXEnd   = centerPeak + half;

                // Clip values are in source-peak units (they slice srcXStart/srcXEnd).
                // For the dst rect they need to be in *screen-pixel* units, which
                // differ from source-pixels by a factor of _playbackSpeed
                // (1 screen pixel = _playbackSpeed source peaks at scale time).
                // Forgetting this conversion makes the waveform drift away from
                // the live-drawn downbeat lines whenever there's edge clipping at
                // non-unity tempo — exactly the "first play, crank tempo" repro.
                float clipLeftSrc  = srcXStart < 0 ? -srcXStart : 0;
                float clipRightSrc = srcXEnd > totalPeaks ? srcXEnd - totalPeaks : 0;
                float clipLeftDst  = (float)(clipLeftSrc  / _playbackSpeed);
                float clipRightDst = (float)(clipRightSrc / _playbackSpeed);

                float validSrcW = (srcXEnd - srcXStart) - clipLeftSrc - clipRightSrc;
                if (validSrcW > 0)
                {
                    var src = new SKRect(srcXStart + clipLeftSrc, 0, srcXEnd - clipRightSrc, _image.Height);
                    var dst = new SKRect(clipLeftDst, 0, dstW - clipRightDst, dstH);
                    _blitPaint ??= new SKPaint { FilterQuality = SKFilterQuality.Low };
                    canvas.DrawImage(_image, src, dst, _blitPaint);
                }
            }

            _headPaint ??= new SKPaint { Color = SKColors.White, StrokeWidth = 2, IsAntialias = false };
            int halfX = dstW / 2;
            canvas.DrawLine(halfX, 0, halfX, dstH, _headPaint);

            // Gain overlay: a thin horizontal line where Y = 0 means 100% (top) and
            // Y = dstH means 0%. So gain=1 → top, gain=0 → bottom.
            float gainY = (float)((1.0 - Math.Clamp(_gain, 0, 1)) * dstH);
            _gainPaint ??= new SKPaint { StrokeWidth = 1, IsAntialias = false };
            _gainPaint.Color = _gainColor;
            canvas.DrawLine(0, gainY, dstW, gainY, _gainPaint);

            // Time-mapping reference for every live overlay below. Prefer GridPeaks
            // (the always-on basic peaks) so the beatgrid keeps scrolling when every
            // stem is muted and the stem body has gone empty. Fall back to body peaks
            // if grid peaks haven't landed yet.
            var refPeaks = (_gridPeaks is { Min.Length: > 0 }) ? _gridPeaks
                         : (_peaks is { Min.Length: > 0 })     ? _peaks
                         : null;
            double? refSecondsPerPeak = refPeaks is null ? null
                : refPeaks.SamplesPerPeak / (double)AudioFileDecoder.TargetSampleRate;
            int refTotalPeaks = refPeaks?.Min.Length ?? 0;
            float refCenterPeak = (float)(_playPosition * refTotalPeaks);

            // Full-height downbeat guides — always on. Acts as a fixed yellow grid
            // so the user can eyeball alignment between decks at a glance.
            if (_downbeats is { Length: > 0 } && refSecondsPerPeak is double dbSpp)
            {
                // Vertical lines look fine without AA, and AA on N lines per frame
                // across two decks is a real cost on Skia's CPU rasteriser.
                _dbPaint ??= new SKPaint { StrokeWidth = 2, IsAntialias = false };
                _dbPaint.Color = _downbeatColor;
                var dbPaint = _dbPaint;
                foreach (var t in _downbeats)
                {
                    float beatCol = (float)(t / dbSpp);
                    // Same source→screen mapping as the waveform blit above: 1 screen pixel
                    // shows _playbackSpeed source peaks, so divide the peak offset by speed.
                    float x = (float)((beatCol - refCenterPeak) / _playbackSpeed) + dstW / 2f;
                    if (x >= -2 && x < dstW + 2)
                        canvas.DrawLine(x, 0, x, dstH, dbPaint);
                }
            }

            // Small white beat ticks along the top edge — non-downbeat beats only,
            // downbeats already get the full-height yellow line above. Lives in
            // the live overlay (not the baked image) so it keeps scrolling when
            // the stem body is empty.
            if (_beats is { Length: > 0 } && refSecondsPerPeak is double btSpp)
            {
                _beatTickPaint ??= new SKPaint { Color = new SKColor(0xFF, 0xFF, 0xFF, 0xC0), StrokeWidth = 1, IsAntialias = false };
                // Build a HashSet of downbeat columns once per frame — cheap relative
                // to the per-beat hit test and avoids drawing a downbeat twice.
                HashSet<int>? dbCols = null;
                if (_downbeats is { Length: > 0 })
                {
                    dbCols = new HashSet<int>(_downbeats.Length);
                    foreach (var t in _downbeats) dbCols.Add((int)Math.Round(t / btSpp));
                }
                for (int i = 0; i < _beats.Length; i++)
                {
                    int col = (int)Math.Round(_beats[i] / btSpp);
                    bool isDownbeat = dbCols is not null ? dbCols.Contains(col) : (i % 4) == 0;
                    if (isDownbeat) continue;
                    float x = (float)((col - refCenterPeak) / _playbackSpeed) + dstW / 2f;
                    if (x >= -2 && x < dstW + 2)
                        canvas.DrawLine(x, 0, x, 5, _beatTickPaint);
                }
            }

            // Magnetic glow: when both decks are beat-locked-ish, paint a bright
            // green stripe at the top and bottom of the nearest beat in each deck.
            if (_magneticGlowSec >= 0 && refSecondsPerPeak is double mgSpp)
            {
                float beatCol = (float)(_magneticGlowSec / mgSpp);
                float x = (float)((beatCol - refCenterPeak) / _playbackSpeed) + dstW / 2f;
                if (x >= -2 && x < dstW + 2)
                {
                    _glowPaint ??= new SKPaint { Color = new SKColor(0x34, 0xF0, 0x6F, 0xF0), IsAntialias = false };
                    _glowPaint.StrokeWidth = _isScrubbing ? 3 : 4;
                    var glow = _glowPaint;
                    if (_isScrubbing)
                    {
                        // Full-height guide line while the user is actively turning the
                        // jog wheel — makes it obvious when the two decks' greens align.
                        canvas.DrawLine(x, 0, x, dstH, glow);
                    }
                    else
                    {
                        canvas.DrawLine(x, 0, x, 16, glow);
                        canvas.DrawLine(x, dstH - 16, x, dstH, glow);
                    }
                }
            }
        }
    }
}
