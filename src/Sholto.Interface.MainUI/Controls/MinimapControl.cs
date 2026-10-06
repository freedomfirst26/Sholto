using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using Avalonia.Threading;
using Sholto.Data;
using Sholto.Interface.MainUI.Controls.Minimap;
using Sholto.Interface.MainUI.Controls.WaveformStyles;
using Sholto.Interface.MainUI.Theming;

namespace Sholto.Interface.MainUI.Controls;

/// <summary>
/// Stationary whole-song map, "band overview": a 14-DIP label row (section names over a coloured underline
/// per section, "DROP 16": kind and bars) above a 50-DIP body that is the whole track in the deck's waveform style (3-BAND or RGB,
/// the same <see cref="IWaveformStyleStrategy"/> as the main waveform), squeezed to the strip's width over a
/// faint tint of each section's colour, phrase lines every 8, 16 and 32 bars over it, and dividers at the
/// section boundaries. Sections sit in bars, placed on the strip through the bar grid. Display-only: transport is
/// controller-only (platter, CUE, Shift + CUE).
///
/// The still picture is baked on a background task (<see cref="IMinimapBaker"/>), posted back to the UI thread, and re-baked only when the track, its
/// sections, the strip's pixel size or scale, the minimap palette, or the style or the colours it bakes
/// change; a frame blits it and draws the moving parts over it from cached brushes: the played part
/// dimmed, the loop, cue markers, and the playhead.
///
/// Plain <see cref="DrawingContext"/> primitives. A control created from XAML draws nothing until its
/// palette arrives.
/// </summary>
public sealed class MinimapControl : Control
{
    private const double MarkerHalfWidth = 4;
    private const double MarkerHeight = 5.2;

    // Avalonia constructs this control from XAML with no constructor arguments
    // (see WaveformControl's matching field doc) — instance fields, not static type-name calls.
    private readonly IMinimapGeometry _geometry = new MinimapGeometry();

    /// <summary>The sections in bars; with <see cref="BarPeriodSec"/> they place the blocks on the strip.</summary>
    public static readonly StyledProperty<IReadOnlyList<DeckSection>?> SectionsProperty =
        AvaloniaProperty.Register<MinimapControl, IReadOnlyList<DeckSection>?>(nameof(Sections));

    /// <summary>Where the phrase lines fall: every <c>PhraseBars</c> bars from <c>PhaseBar</c>.</summary>
    public static readonly StyledProperty<DeckPhraseGrid> PhraseGridProperty =
        AvaloniaProperty.Register<MinimapControl, DeckPhraseGrid>(nameof(PhraseGrid), new DeckPhraseGrid(0, 8));

    /// <summary>Time of bar 0 in seconds.</summary>
    public static readonly StyledProperty<double> FirstDownbeatSecProperty =
        AvaloniaProperty.Register<MinimapControl, double>(nameof(FirstDownbeatSec));

    /// <summary>Seconds per bar; 0 when the track has no grid.</summary>
    public static readonly StyledProperty<double> BarPeriodSecProperty =
        AvaloniaProperty.Register<MinimapControl, double>(nameof(BarPeriodSec));

    public static readonly StyledProperty<WaveformPeaks?> PeaksProperty =
        AvaloniaProperty.Register<MinimapControl, WaveformPeaks?>(nameof(Peaks));

    public static readonly StyledProperty<double> PlayPositionProperty =
        AvaloniaProperty.Register<MinimapControl, double>(nameof(PlayPosition));

    /// <summary>Cue positions in seconds, drawn as small down-triangles on the top edge.</summary>
    public static readonly StyledProperty<double[]?> MarkerSecsProperty =
        AvaloniaProperty.Register<MinimapControl, double[]?>(nameof(MarkerSecs));

    public static readonly StyledProperty<double?> LoopStartSecProperty =
        AvaloniaProperty.Register<MinimapControl, double?>(nameof(LoopStartSec));

    public static readonly StyledProperty<double?> LoopEndSecProperty =
        AvaloniaProperty.Register<MinimapControl, double?>(nameof(LoopEndSec));

    public static readonly StyledProperty<MinimapPalette?> PaletteProperty =
        AvaloniaProperty.Register<MinimapControl, MinimapPalette?>(nameof(Palette));

    /// <summary>The deck waveform's palette: the wave's colours, and the marker and loop colours.</summary>
    public static readonly StyledProperty<WaveformPalette?> WaveformPaletteProperty =
        AvaloniaProperty.Register<MinimapControl, WaveformPalette?>(nameof(WaveformPalette));

    /// <summary>How the wave is drawn (3-BAND, RGB), bound to the same resource as the main waveform's.</summary>
    public static readonly StyledProperty<IWaveformStyleStrategy?> StyleStrategyProperty =
        AvaloniaProperty.Register<MinimapControl, IWaveformStyleStrategy?>(nameof(StyleStrategy));

    // Cached live paint, retoned in place when the palettes change.
    private readonly SolidColorBrush _backdrop = new();
    private readonly SolidColorBrush _played = new();
    private readonly SolidColorBrush _playhead = new();
    private readonly SolidColorBrush _playheadHalo = new();
    private readonly SolidColorBrush _marker = new();
    private readonly SolidColorBrush _loopFill = new();
    private readonly SolidColorBrush _loopEdge = new();
    private readonly StreamGeometry _markerShape = new();

    // The picture on screen, and what the strip's size and scale are now. Bakes run off the UI thread, so
    // the picture can trail the request: until a new one lands the last one is drawn, stretched to fit.
    private WriteableBitmap? _baked;
    private Rect _bakedSource;
    private bool _dirty = true;
    private int _wantedPixelWidth, _wantedPixelHeight;
    private double _wantedScale = 1;
    private WaveformPalette? _bakedWaveformPalette;
    private double _trackSecs;

    // Bake bookkeeping, UI thread only: one bake in flight at a time, the newest request waits behind it,
    // and a result applies only if no newer request has been made since it started.
    private int _generation;
    private bool _bakeInFlight;
    private BakeRequest? _pendingRequest;

    private sealed record BakeRequest(
        int Generation, WaveformPeaks Peaks, MinimapStructure? Structure, MinimapPalette Palette,
        WaveformPalette Waveform, IWaveformStyleStrategy Style, int PixelWidth, int PixelHeight, double Scale);

    static MinimapControl()
    {
        AffectsRender<MinimapControl>(PlayPositionProperty, MarkerSecsProperty,
            LoopStartSecProperty, LoopEndSecProperty, PaletteProperty, WaveformPaletteProperty);
        PeaksProperty.Changed.AddClassHandler<MinimapControl>((c, _) => c.OnPeaksChanged());
        SectionsProperty.Changed.AddClassHandler<MinimapControl>((c, _) => c.Invalidate());
        PhraseGridProperty.Changed.AddClassHandler<MinimapControl>((c, _) => c.Invalidate());
        FirstDownbeatSecProperty.Changed.AddClassHandler<MinimapControl>((c, _) => c.Invalidate());
        BarPeriodSecProperty.Changed.AddClassHandler<MinimapControl>((c, _) => c.Invalidate());
        PaletteProperty.Changed.AddClassHandler<MinimapControl>((c, _) => c.OnPaletteChanged());
        WaveformPaletteProperty.Changed.AddClassHandler<MinimapControl>((c, _) => c.OnWaveformPaletteChanged());
        StyleStrategyProperty.Changed.AddClassHandler<MinimapControl>((c, _) => c.Invalidate());
    }

    public MinimapControl()
    {
        // Display-only: transport is controller-only, so clicks fall through
        // and no hand cursor appears over the strip.
        IsHitTestVisible = false;
        // The baked picture is already one pixel per device pixel; never smooth it.
        RenderOptions.SetBitmapInterpolationMode(this, BitmapInterpolationMode.None);
        using (var ctx = _markerShape.Open())
        {
            ctx.BeginFigure(new Point(-MarkerHalfWidth, 0), true);
            ctx.LineTo(new Point(MarkerHalfWidth, 0));
            ctx.LineTo(new Point(0, MarkerHeight));
            ctx.EndFigure(true);
        }
    }

    public IReadOnlyList<DeckSection>? Sections { get => GetValue(SectionsProperty); set => SetValue(SectionsProperty, value); }
    public DeckPhraseGrid PhraseGrid { get => GetValue(PhraseGridProperty); set => SetValue(PhraseGridProperty, value); }
    public double FirstDownbeatSec { get => GetValue(FirstDownbeatSecProperty); set => SetValue(FirstDownbeatSecProperty, value); }
    public double BarPeriodSec { get => GetValue(BarPeriodSecProperty); set => SetValue(BarPeriodSecProperty, value); }
    public WaveformPeaks? Peaks { get => GetValue(PeaksProperty); set => SetValue(PeaksProperty, value); }
    public double PlayPosition { get => GetValue(PlayPositionProperty); set => SetValue(PlayPositionProperty, value); }
    public double[]? MarkerSecs { get => GetValue(MarkerSecsProperty); set => SetValue(MarkerSecsProperty, value); }
    public double? LoopStartSec { get => GetValue(LoopStartSecProperty); set => SetValue(LoopStartSecProperty, value); }
    public double? LoopEndSec { get => GetValue(LoopEndSecProperty); set => SetValue(LoopEndSecProperty, value); }
    public MinimapPalette? Palette { get => GetValue(PaletteProperty); set => SetValue(PaletteProperty, value); }
    public WaveformPalette? WaveformPalette { get => GetValue(WaveformPaletteProperty); set => SetValue(WaveformPaletteProperty, value); }
    public IWaveformStyleStrategy? StyleStrategy { get => GetValue(StyleStrategyProperty); set => SetValue(StyleStrategyProperty, value); }

    /// <summary>The baker; replaceable so a test can count or stub bakes. XAML cannot inject, so it
    /// defaults to the real one.</summary>
    internal IMinimapBaker Baker { get; set; } = new MinimapBaker(
        new MinimapPeakDownsampler(), new MinimapGeometry(), new MinimapPhraseLines(), new MinimapSectionLabels());

    /// <summary>Device pixels per DIP, when a test has no window to take it from.</summary>
    internal double? ScaleOverride { get; set; }

    /// <summary>How many bakes have completed (applied or dropped as stale).</summary>
    internal int BakeCount { get; private set; }

    /// <summary>True from a bake starting until its result has been posted back and handled.</summary>
    internal bool BakeInFlight => _bakeInFlight;

    /// <summary>How a finished bake is handed to the UI thread; replaceable so a test can run it itself.</summary>
    internal Action<Action> Post { get; set; } = work => Dispatcher.UIThread.Post(work);

    /// <summary>The baked picture's size in device pixels, or default when nothing is baked.</summary>
    internal PixelSize BakedPixelSize => _baked?.PixelSize ?? default;

    internal WriteableBitmap? BakedBitmap => _baked;

    private void OnPeaksChanged()
    {
        var pk = Peaks;
        _trackSecs = pk is { Min.Length: > 0 }
            ? pk.Min.Length * pk.SecondsPerPeak
            : 0;
        _baked = null;   // another track's picture must not stand in while this one bakes
        Invalidate();
    }

    private void Invalidate()
    {
        _dirty = true;
        InvalidateVisual();
    }

    private void OnPaletteChanged()
    {
        if (Palette is { } p)
        {
            _backdrop.Color = p.Backdrop;
            _played.Color = WithAlpha(p.Backdrop, 0.55);
            _playhead.Color = p.Playhead;
            _playheadHalo.Color = WithAlpha(p.Backdrop, 1);
        }
        Invalidate();
    }

    // The wave only bakes the colours its style uses, so a theme that keeps them keeps the picture; the
    // marker and loop colours are live.
    private void OnWaveformPaletteChanged()
    {
        if (WaveformPalette is { } w)
        {
            _marker.Color = WithAlpha(w.Marker, 1);
            _loopFill.Color = WithAlpha(w.Loop, 0.3);
            _loopEdge.Color = WithAlpha(w.Loop, 1);
        }
        if (_bakedWaveformPalette is null || WaveformPalette is null || StyleStrategy is not { } style
            || !style.BakesSameColours(_bakedWaveformPalette, WaveformPalette))
            _dirty = true;
        InvalidateVisual();
    }

    private Color WithAlpha(Color c, double alpha) => Color.FromArgb((byte)Math.Round(alpha * 255), c.R, c.G, c.B);

    protected override Size MeasureOverride(Size availableSize)
    {
        double w = double.IsInfinity(availableSize.Width) ? 0 : availableSize.Width;
        return new Size(w, MinimapMetrics.StripHeight);
    }

    /// <summary>Ask for a new still picture if anything it depends on changed since the last request. A frame
    /// that changed none of them only compares a few numbers; a changed one snapshots the inputs and bakes on
    /// a background task, and the finished bitmap is posted back to the UI thread.</summary>
    internal void EnsureBaked()
    {
        double scale = ScaleOverride ?? TopLevel.GetTopLevel(this)?.RenderScaling ?? 1.0;
        int pxW = (int)Math.Ceiling(Bounds.Width * scale);
        int pxH = (int)Math.Round(Bounds.Height * scale);
        if (!_dirty && pxW == _wantedPixelWidth && pxH == _wantedPixelHeight && scale == _wantedScale) return;
        _dirty = false;
        _wantedPixelWidth = pxW;
        _wantedPixelHeight = pxH;
        _wantedScale = scale;

        // Any bake already running is now stale: its result will be dropped.
        int generation = ++_generation;
        if (Peaks is not { Min.Length: > 0 } peaks || Palette is not { } palette
            || WaveformPalette is not { } waveform || StyleStrategy is not { } style)
        {
            _baked = null;
            _pendingRequest = null;
            return;
        }

        _pendingRequest = new BakeRequest(generation, peaks, Structure(), palette, waveform, style, pxW, pxH, scale);
        if (!_bakeInFlight) StartPendingBake();
    }

    private void StartPendingBake()
    {
        var request = _pendingRequest!;
        _pendingRequest = null;
        _bakeInFlight = true;
        var baker = Baker;
        var post = Post;
        Task.Run(() =>
        {
            WriteableBitmap? bitmap = null;
            try
            {
                using var image = baker.Bake(request.Peaks, request.Structure, request.Palette, request.Waveform,
                    request.Style, request.PixelWidth, request.PixelHeight, request.Scale);
                bitmap = image?.ToBitmap();
            }
            catch (Exception)
            {
                // A style that cannot draw these peaks leaves the strip on its backdrop, as a null bake does.
            }
            post(() => FinishBake(request, bitmap));
        });
    }

    private void FinishBake(BakeRequest request, WriteableBitmap? bitmap)
    {
        _bakeInFlight = false;
        BakeCount++;
        if (request.Generation == _generation)
        {
            _baked = bitmap;
            _bakedWaveformPalette = request.Waveform;
            if (bitmap is not null) _bakedSource = new Rect(0, 0, bitmap.PixelSize.Width, bitmap.PixelSize.Height);
            InvalidateVisual();
        }
        // else stale: a newer request exists, so this picture is dropped (left to the GC, as the
        // render thread may still hold the one it replaces).
        if (_pendingRequest is not null) StartPendingBake();
    }

    private MinimapStructure? Structure() =>
        Sections is { Count: > 0 } sections
            ? new MinimapStructure(sections, PhraseGrid, FirstDownbeatSec, BarPeriodSec)
            : null;

    public override void Render(DrawingContext context)
    {
        double w = Bounds.Width, h = Bounds.Height;
        if (w <= 0 || h <= 0) return;

        // No fallback palette: draw nothing until the bound Palette arrives.
        if (Palette is null) return;

        EnsureBaked();
        var strip = new Rect(0, 0, w, h);
        if (_baked is null)
        {
            context.FillRectangle(_backdrop, strip);
            return;
        }
        // Draw at exactly the baked size in device pixels (1:1, whatever the render scale). While a new bake
        // is on its way the last picture is stretched over the strip instead.
        bool exact = _baked.PixelSize.Width == _wantedPixelWidth && _baked.PixelSize.Height == _wantedPixelHeight;
        context.DrawImage(_baked, _bakedSource, exact
            ? new Rect(0, 0, _bakedSource.Width / _wantedScale, _bakedSource.Height / _wantedScale)
            : strip);

        double px = _geometry.X(PlayPosition, w);
        context.FillRectangle(_played, new Rect(0, 0, px, h));

        if (WaveformPalette is not null)
        {
            if (LoopStartSec is double loopStart && LoopEndSec is double loopEnd
                && _geometry.Region(loopStart, loopEnd, _trackSecs, w) is { } loop)
            {
                context.FillRectangle(_loopFill, new Rect(loop.Left, 0, loop.Width, h));
                context.FillRectangle(_loopEdge, new Rect(loop.Left, 0, 1, h));
                context.FillRectangle(_loopEdge, new Rect(Math.Max(loop.Left, loop.Right - 1), 0, 1, h));
            }

            if (MarkerSecs is { Length: > 0 } markers)
            {
                for (int i = 0; i < markers.Length; i++)
                {
                    double mx = _geometry.XOfSeconds(markers[i], _trackSecs, w);
                    using (context.PushTransform(Matrix.CreateTranslation(mx, 0)))
                        context.DrawGeometry(_marker, null, _markerShape);
                }
            }
        }

        // Snap to whole device pixels (the bake is drawn 1:1 in device pixels, so a DIP-rounded line blurs
        // at fractional render scales): a 4 px backdrop halo under a 2 px playhead, 1 px outline each side.
        double devLeft = Math.Round(px * _wantedScale) - 1;
        context.FillRectangle(_playheadHalo, new Rect((devLeft - 1) / _wantedScale, 0, 4 / _wantedScale, h));
        context.FillRectangle(_playhead, new Rect(devLeft / _wantedScale, 0, 2 / _wantedScale, h));
    }
}
