using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;
using Avalonia.Rendering.SceneGraph;
using Avalonia.Skia;
using Avalonia.Threading;
using Sholto.Interface.MainUI.Controls.WaveformStyles;
using Sholto.Interface.MainUI.Theming;
using SkiaSharp;
using Sholto.Data;

namespace Sholto.Interface.MainUI.Controls;

/// <summary>
/// Pre-renders the entire waveform to an offscreen SKImage at track load,
/// then blits a window per frame. One textured rectangle per frame instead of
/// thousands of DrawLine calls.
/// </summary>
public sealed class WaveformControl : Control
{
    // Render-thread paint cache, one per control (see WaveformPaints).
    private readonly WaveformPaints _paints = new();
    // Whole-device-pixel scroll snapping, so the baked body does not flicker as it scrolls (see the class).
    private readonly WaveformScrollSnap _snap = new();

    public static readonly StyledProperty<WaveformPeaks?> PeaksProperty =
        AvaloniaProperty.Register<WaveformControl, WaveformPeaks?>(nameof(Peaks));

    /// <summary>Mixed-track peaks used purely as the time→pixel reference for the
    /// scrolling beatgrid and live overlays. Stays populated even when every stem
    /// is muted, so the grid keeps moving while <see cref="Peaks"/> is empty.</summary>
    public static readonly StyledProperty<WaveformPeaks?> GridPeaksProperty =
        AvaloniaProperty.Register<WaveformControl, WaveformPeaks?>(nameof(GridPeaks));

    /// <summary>Vocal-presence regions (from <c>VocalRegionAnalyzer</c>). Not a
    /// waveform — the control paints one solid green rectangle per span on top of the
    /// band waveform, so you can see where the vocals sit at a glance. Null until
    /// stems land.</summary>
    public static readonly StyledProperty<IReadOnlyList<VocalRegion>?> VocalRegionsProperty =
        AvaloniaProperty.Register<WaveformControl, IReadOnlyList<VocalRegion>?>(nameof(VocalRegions));

    /// <summary>Whether the vocal stem is currently audible. The vocal-presence
    /// rectangles are green when it's on and grey when it's muted — so muting VOX
    /// visibly greys out where the vocals are. Default true.</summary>
    public static readonly StyledProperty<bool> VocalsActiveProperty =
        AvaloniaProperty.Register<WaveformControl, bool>(nameof(VocalsActive), true);

    /// <summary>Marker positions (seconds) to flag on the waveform — saved cue points
    /// (Rekordbox-style memory cues). Drawn as thin vertical lines with a top flag.</summary>
    public static readonly StyledProperty<double[]?> MarkerSecsProperty =
        AvaloniaProperty.Register<WaveformControl, double[]?>(nameof(MarkerSecs));

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

    public static readonly StyledProperty<double> GainOverlayProperty =
        AvaloniaProperty.Register<WaveformControl, double>(nameof(GainOverlay), 1.0);

    // Whether the channel gain has actually been measured. Until the FLX-4 fader is
    // touched we don't know it, so the gain line is not drawn (we never render an
    // unmeasured value). Default false.
    public static readonly StyledProperty<bool> GainKnownProperty =
        AvaloniaProperty.Register<WaveformControl, bool>(nameof(GainKnown), false);

    public static readonly StyledProperty<double> MagneticGlowSecProperty =
        AvaloniaProperty.Register<WaveformControl, double>(nameof(MagneticGlowSec), -1.0);

    public static readonly StyledProperty<bool> IsScrubbingProperty =
        AvaloniaProperty.Register<WaveformControl, bool>(nameof(IsScrubbing), false);

    /// <summary>Every colour this control draws, from the active theme
    /// (bound via DynamicResource SholtoWaveformPalette). Null until it
    /// arrives (MainWindow sets it before render); the control draws nothing while null.</summary>
    public static readonly StyledProperty<WaveformPalette?> PaletteProperty =
        AvaloniaProperty.Register<WaveformControl, WaveformPalette?>(nameof(Palette));
    public WaveformPalette? Palette { get => GetValue(PaletteProperty); set => SetValue(PaletteProperty, value); }

    /// <summary>How the waveform body is drawn (3-BAND, RGB, …), bound via DynamicResource
    /// SholtoWaveformStyle, which MainWindow publishes from the view model's chosen style. Null until it
    /// arrives; the control bakes nothing while null. Changing it rebakes; the live overlays are the same
    /// for every style.</summary>
    public static readonly StyledProperty<IWaveformStyleStrategy?> StyleStrategyProperty =
        AvaloniaProperty.Register<WaveformControl, IWaveformStyleStrategy?>(nameof(StyleStrategy));
    public IWaveformStyleStrategy? StyleStrategy { get => GetValue(StyleStrategyProperty); set => SetValue(StyleStrategyProperty, value); }

    /// <summary>True once the user has nudged this deck's beatgrid; the loop band
    /// turns red so the grid edit is visible. Replaces LoopColorConverter.</summary>
    public static readonly StyledProperty<bool> IsGridNudgedProperty =
        AvaloniaProperty.Register<WaveformControl, bool>(nameof(IsGridNudged));
    public bool IsGridNudged { get => GetValue(IsGridNudgedProperty); set => SetValue(IsGridNudgedProperty, value); }


    private SKColor Sk(Avalonia.Media.Color c) => new(c.R, c.G, c.B, c.A);

    /// <summary>Loop-in point in seconds, or null = no loop. The control paints
    /// a translucent band between this and <see cref="LoopEndSec"/>.</summary>
    public static readonly StyledProperty<double?> LoopStartSecProperty =
        AvaloniaProperty.Register<WaveformControl, double?>(nameof(LoopStartSec));

    /// <summary>Loop-out point in seconds, or null = no loop.</summary>
    public static readonly StyledProperty<double?> LoopEndSecProperty =
        AvaloniaProperty.Register<WaveformControl, double?>(nameof(LoopEndSec));

    /// <summary>When true, clicking the waveform raises <see cref="GridAnchorClicked"/>
    /// with the clicked track-time instead of doing nothing — the two-point
    /// grid-edit gesture. Also tints the control border so the mode is obvious.</summary>
    public static readonly StyledProperty<bool> GridEditModeProperty =
        AvaloniaProperty.Register<WaveformControl, bool>(nameof(GridEditMode), false);

    public bool GridEditMode
    {
        get => GetValue(GridEditModeProperty);
        set => SetValue(GridEditModeProperty, value);
    }

    /// <summary>Raised on click while <see cref="GridEditMode"/> is true.
    /// Argument is the clicked position in track seconds.</summary>
    public event Action<double>? GridAnchorClicked;

    private SKImage? _baked;
    private WaveformPeaks? _bakedFor;
    private CancellationTokenSource? _bakeCts;

    // Contiguous vocal-present spans in track SECONDS (start, end), derived from
    // VocalPeaks whenever it changes. Precomputed (not per-frame) since it's a
    // full pass over the stem's per-column envelope; the render loop just maps
    // each span through the same time→screen transform the beatgrid uses.
    // null = vocal analysis hasn't landed (draw no lane); empty = analyzed, no
    // vocals found (draw the all-grey lane).
    private (double Start, double End)[]? _vocalRegions;

    static WaveformControl()
    {
        AffectsRender<WaveformControl>(GridEditModeProperty);
        AffectsRender<WaveformControl>(PlayPositionProperty);
        AffectsRender<WaveformControl>(PlaybackSpeedProperty);
        AffectsRender<WaveformControl>(GainOverlayProperty);
        AffectsRender<WaveformControl>(GainKnownProperty);
        AffectsRender<WaveformControl>(MagneticGlowSecProperty);
        AffectsRender<WaveformControl>(IsScrubbingProperty);
        PeaksProperty.Changed.AddClassHandler<WaveformControl>((c, _) => c.Rebake());
        PaletteProperty.Changed.AddClassHandler<WaveformControl>((c, _) => c.OnPaletteChanged());
        StyleStrategyProperty.Changed.AddClassHandler<WaveformControl>((c, _) => c.Rebake());
        AffectsRender<WaveformControl>(IsGridNudgedProperty);
        // Vocal overlay is drawn live (not baked); cache the incoming spans then
        // invalidate so the next frame paints the green rectangles.
        VocalRegionsProperty.Changed.AddClassHandler<WaveformControl>((c, _) => c.OnVocalRegionsChanged());
        AffectsRender<WaveformControl>(VocalsActiveProperty);
        AffectsRender<WaveformControl>(MarkerSecsProperty);
        // Beatgrid is drawn live (not baked), so it doesn't need a rebake — just
        // an invalidate so the next frame picks up the new ticks.
        AffectsRender<WaveformControl>(GridPeaksProperty);
        AffectsRender<WaveformControl>(BeatTimesProperty);
        AffectsRender<WaveformControl>(DownbeatTimesProperty);
        AffectsRender<WaveformControl>(LoopStartSecProperty);
        AffectsRender<WaveformControl>(LoopEndSecProperty);
    }

    // Only the baked image depends on the colours the active style bakes;
    // everything else is drawn live. Rebake only when one of those actually
    // changed, so a theme switch that keeps them doesn't redo the whole bake.
    private WaveformPalette? _bakedPalette;
    private void OnPaletteChanged()
    {
        if (Palette is not { } p) { InvalidateVisual(); return; }
        if (_bakedPalette is null || StyleStrategy is not { } style || !style.BakesSameColours(_bakedPalette, p)) Rebake();
        else InvalidateVisual();
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

    public IReadOnlyList<VocalRegion>? VocalRegions
    {
        get => GetValue(VocalRegionsProperty);
        set => SetValue(VocalRegionsProperty, value);
    }

    public bool VocalsActive
    {
        get => GetValue(VocalsActiveProperty);
        set => SetValue(VocalsActiveProperty, value);
    }

    public double[]? MarkerSecs
    {
        get => GetValue(MarkerSecsProperty);
        set => SetValue(MarkerSecsProperty, value);
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

    public bool GainKnown
    {
        get => GetValue(GainKnownProperty);
        set => SetValue(GainKnownProperty, value);
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

    public double? LoopStartSec
    {
        get => GetValue(LoopStartSecProperty);
        set => SetValue(LoopStartSecProperty, value);
    }

    public double? LoopEndSec
    {
        get => GetValue(LoopEndSecProperty);
        set => SetValue(LoopEndSecProperty, value);
    }

    /// <summary>Cache the analyzer's spans in render-friendly form. The presence
    /// detection itself lives in <c>VocalRegionAnalyzer</c> — here we just
    /// project to (start, end) seconds and invalidate.</summary>
    private void OnVocalRegionsChanged()
    {
        var vr = VocalRegions;
        _vocalRegions = vr is null
            ? null
            : vr.Select(r => (r.StartSec, r.EndSec)).ToArray();
        InvalidateVisual();
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
        if (Palette is not { } p) return;
        if (StyleStrategy is not { } style) return;
        Task.Run(() =>
        {
            var img = style.Bake(snapshot, p, cts.Token);
            if (cts.IsCancellationRequested || img is null) { img?.Dispose(); return; }
            Dispatcher.UIThread.Post(() =>
            {
                if (cts.IsCancellationRequested) { img.Dispose(); return; }
                _baked = img;
                _bakedFor = snapshot;
                _bakedPalette = p;
                InvalidateVisual();
            });
        });
    }

    public override void Render(DrawingContext context)
    {
        if (Palette is not { } p) return;
        var bgColor       = Sk(p.Background);
        var downbeatColor = Sk(p.Downbeat);
        var gainColor     = Sk(p.Gain);
        var loopColor     = IsGridNudged ? Sk(p.GridEdit).WithAlpha(0xA0) : Sk(p.Loop);   // red once the grid was nudged
        var live = new LiveColours(Sk(p.Playhead), Sk(p.BeatTick), Sk(p.Marker),
            Sk(p.Vocal).WithAlpha(0xD0), Sk(p.VocalInactive).WithAlpha(0xB0), Sk(p.SnapGlow).WithAlpha(0xF0), Sk(p.Edge).WithAlpha(0x99));
        context.Custom(new BlitOperation(_paints, _snap, new Rect(Bounds.Size), _baked, _bakedFor, GridPeaks, PlayPosition, PlaybackSpeed, GainOverlay, GainKnown, MagneticGlowSec, IsScrubbing, BeatTimes, DownbeatTimes, LoopStartSec, LoopEndSec, downbeatColor, gainColor, loopColor, _vocalRegions, VocalsActive, MarkerSecs, bgColor, live));

        // Grid-edit mode: red border tint so the user knows clicks set anchors.
        if (GridEditMode)
        {
            var pen = new Avalonia.Media.Pen(new Avalonia.Media.SolidColorBrush(
                Avalonia.Media.Color.FromArgb(0xE0, p.GridEdit.R, p.GridEdit.G, p.GridEdit.B)), 2);
            context.DrawRectangle(null, pen, new Rect(Bounds.Size));
        }
    }

    protected override void OnPointerPressed(Avalonia.Input.PointerPressedEventArgs e)
    {
        base.OnPointerPressed(e);
        if (!GridEditMode) return;

        // Map click X → track seconds using the SAME source→screen mapping the
        // overlays use (see Render): screen_x = (beatCol − refCenterPeak)/speed
        // + width/2, so beatCol = (x − width/2)·speed + refCenterPeak, and
        // seconds = beatCol · secondsPerPeak. Reference peaks = GridPeaks
        // (falls back to Peaks) so this matches where the grid lines render.
        var refPeaks = (GridPeaks is { Min.Length: > 0 }) ? GridPeaks
                     : (Peaks is { Min.Length: > 0 })     ? Peaks
                     : null;
        if (refPeaks is null) return;

        double secondsPerPeak = refPeaks.SecondsPerPeak;
        int total = refPeaks.Min.Length;
        double centerPeak = PlayPosition * total;
        double speed = PlaybackSpeed > 0.01 ? PlaybackSpeed : 1.0;
        double w = Bounds.Width;
        double x = e.GetPosition(this).X;

        double beatCol = (x - w / 2.0) * speed + centerPeak;
        double seconds = beatCol * secondsPerPeak;
        if (seconds < 0) seconds = 0;
        GridAnchorClicked?.Invoke(seconds);
        e.Handled = true;
    }

    private readonly record struct LiveColours(SKColor Playhead, SKColor BeatTick, SKColor Marker, SKColor VocalActive, SKColor VocalInactive, SKColor Glow, SKColor Edge);

    private sealed class BlitOperation : ICustomDrawOperation
    {
        // Paints come from the control's WaveformPaints (per-thread cached, no per-frame allocation).

        private readonly WaveformPaints _paints;
        private readonly WaveformScrollSnap _snap;
        private readonly SKImage? _image;
        private readonly WaveformPeaks? _peaks;
        private readonly WaveformPeaks? _gridPeaks;
        private readonly double _playPosition;
        private readonly double _playbackSpeed;
        private readonly double _gain;
        private readonly bool _gainKnown;
        private readonly double _magneticGlowSec;
        private readonly bool _isScrubbing;
        private readonly double[]? _beats;
        private readonly double[]? _downbeats;
        private readonly double? _loopStartSec;
        private readonly double? _loopEndSec;
        private readonly SKColor _downbeatColor;
        private readonly SKColor _gainColor;
        private readonly SKColor _loopColor;
        private readonly (double Start, double End)[]? _vocalRegions;
        private readonly bool _vocalsActive;
        private readonly double[]? _markerSecs;
        private readonly SKColor _bgColor;
        private readonly LiveColours _live;

        public BlitOperation(WaveformPaints paints, WaveformScrollSnap snap, Rect bounds, SKImage? image, WaveformPeaks? peaks, WaveformPeaks? gridPeaks, double playPosition, double playbackSpeed, double gain, bool gainKnown, double magneticGlowSec, bool isScrubbing, double[]? beats, double[]? downbeats, double? loopStartSec, double? loopEndSec, SKColor downbeatColor, SKColor gainColor, SKColor loopColor, (double Start, double End)[]? vocalRegions, bool vocalsActive, double[]? markerSecs, SKColor bgColor, LiveColours live)
        {
            _paints = paints;
            _snap = snap;
            Bounds = bounds;
            _image = image;
            _peaks = peaks;
            _gridPeaks = gridPeaks;
            _playPosition = playPosition;
            // Guard: never let a runaway 0 collapse the window to zero width.
            _playbackSpeed = playbackSpeed > 0.01 ? playbackSpeed : 1.0;
            _gain = gain;
            _gainKnown = gainKnown;
            _magneticGlowSec = magneticGlowSec;
            _isScrubbing = isScrubbing;
            _beats = beats;
            _downbeats = downbeats;
            _loopStartSec = loopStartSec;
            _loopEndSec = loopEndSec;
            _downbeatColor = downbeatColor;
            _gainColor = gainColor;
            _loopColor = loopColor;
            _vocalRegions = vocalRegions;
            _vocalsActive = vocalsActive;
            _markerSecs = markerSecs;
            _bgColor = bgColor;
            _live = live;
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
            canvas.Clear(_bgColor);

            // Snap the scroll to whole device pixels (see WaveformScrollSnap) and draw EVERYTHING —
            // body and overlays — from the snapped position, so they stay locked together.
            double playPosition = _playPosition;
            if (_image is not null && _peaks is not null && _peaks.Min.Length > 0)
            {
                var device = canvas.TotalMatrix;
                playPosition = _snap.SnapPosition(_playPosition, _peaks.Min.Length, dstW, _playbackSpeed, device.ScaleX, device.TransX);
            }

            if (_image is not null && _peaks is not null && _peaks.Min.Length > 0)
            {
                int totalPeaks = _peaks.Min.Length;
                float centerPeak = (float)(playPosition * totalPeaks); // keep sub-pixel precision
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
                    canvas.DrawImage(_image, src, dst, _paints.Blit);
                }
            }

            // Vocal-presence rectangles — the 4th layer, drawn over the frequency
            // bands. Solid green blocks wherever the isolated vocal stem is active,
            // so you can see the vocals in the track at a glance. Deliberately NOT a
            // waveform — a flat block per contiguous vocal span. The playhead and
            // beatgrid draw on top, so they stay readable. Same time→screen mapping
            // as the grid, so the blocks stay locked at any tempo.
            if (_vocalRegions is { Length: > 0 })
            {
                var vref = (_gridPeaks is { Min.Length: > 0 }) ? _gridPeaks
                         : (_peaks is { Min.Length: > 0 })     ? _peaks : null;
                if (vref is not null)
                {
                    int vtotal = vref.Min.Length;
                    float vcenter = (float)(playPosition * vtotal);
                    double vspp = vref.SecondsPerPeak;
                    // Green when the vocal stem is audible, grey when it's muted — so
                    // muting VOX greys out the sections where the vocals are.
                    _paints.Vocal.Color = _vocalsActive ? _live.VocalActive : _live.VocalInactive;
                    const float bandH = 6f; // a thick line centred in the lane, not a tall block
                    float y0 = (dstH - bandH) / 2f;

                    foreach (var (s, e) in _vocalRegions)
                    {
                        // Column-index → screen-x (same transform as the waveform blit/grid).
                        float x0 = (float)(((s / vspp) - vcenter) / _playbackSpeed) + dstW / 2f;
                        float x1 = (float)(((e / vspp) - vcenter) / _playbackSpeed) + dstW / 2f;
                        if (x1 <= 0 || x0 >= dstW) continue;
                        float cx0 = Math.Max(0, x0), cx1 = Math.Min(dstW, x1);
                        canvas.DrawRect(cx0, y0, cx1 - cx0, bandH, _paints.Vocal);
                    }
                }
            }

            // Active loop band — translucent stripe spanning the loop region.
            // Drawn before the playhead so the head stays on top; uses the same
            // refPeaks time mapping as the beatgrid, so it stays locked at any
            // tempo and survives all-stems-off (the band itself doesn't depend
            // on a body waveform existing).
            var loopTimeRef = (_gridPeaks is { Min.Length: > 0 }) ? _gridPeaks
                            : (_peaks is { Min.Length: > 0 })     ? _peaks
                            : null;
            if (_loopStartSec is double loopS && _loopEndSec is double loopE
                && loopE > loopS && loopTimeRef is not null)
            {
                double lpSpp = loopTimeRef.SecondsPerPeak;
                int totalPeaks = loopTimeRef.Min.Length;
                float lpCenter = (float)(playPosition * totalPeaks);
                float xStart = (float)(((loopS / lpSpp) - lpCenter) / _playbackSpeed) + dstW / 2f;
                float xEnd   = (float)(((loopE / lpSpp) - lpCenter) / _playbackSpeed) + dstW / 2f;
                if (xEnd > 0 && xStart < dstW)
                {
                    float x0 = Math.Max(0, xStart);
                    float x1 = Math.Min(dstW, xEnd);
                    _paints.Loop.Color = _loopColor;
                    canvas.DrawRect(x0, 0, x1 - x0, dstH, _paints.Loop);

                    // Bright edge stripes at loop-in and loop-out — full-opacity
                    // accent so the region reads as a clearly bounded box rather
                    // than a vague tint. Only draws the edge if it's actually
                    // on-screen (clipped sides skip it).
                    var edgeColor = new SKColor(_loopColor.Red, _loopColor.Green, _loopColor.Blue, 0xFF);
                    _paints.Loop.Color = edgeColor;
                    _paints.Loop.Style = SKPaintStyle.Stroke;
                    _paints.Loop.StrokeWidth = 2;
                    if (xStart >= 0)     canvas.DrawLine(xStart, 0, xStart, dstH, _paints.Loop);
                    if (xEnd   <= dstW)  canvas.DrawLine(xEnd,   0, xEnd,   dstH, _paints.Loop);
                    _paints.Loop.Style = SKPaintStyle.Fill; // restore for next frame
                }
            }

            // Dark halo (5 px, background colour) under the 3 px playhead: a 1 px outline each side.
            // Odd widths centred on the middle pixel's centre keep both edges on whole device pixels.
            _paints.HeadHalo.Color = _bgColor;
            _paints.Head.Color = _live.Playhead;
            float headX = dstW / 2 + 0.5f;
            canvas.DrawLine(headX, 0, headX, dstH, _paints.HeadHalo);
            canvas.DrawLine(headX, 0, headX, dstH, _paints.Head);

            // Gain overlay: a thin horizontal line where Y = 0 means 100% (top) and
            // Y = dstH means 0%. So gain=1 → top, gain=0 → bottom. Drawn only once the
            // channel fader has been measured — an unmeasured level is not rendered.
            if (_gainKnown)
            {
                float gainY = (float)((1.0 - Math.Clamp(_gain, 0, 1)) * dstH);
                _paints.Gain.Color = _gainColor;
                canvas.DrawLine(0, gainY, dstW, gainY, _paints.Gain);
            }

            // Time-mapping reference for every live overlay below. Prefer GridPeaks
            // (the always-on basic peaks) so the beatgrid keeps scrolling when every
            // stem is muted and the stem body has gone empty. Fall back to body peaks
            // if grid peaks haven't landed yet.
            var refPeaks = (_gridPeaks is { Min.Length: > 0 }) ? _gridPeaks
                         : (_peaks is { Min.Length: > 0 })     ? _peaks
                         : null;
            double? refSecondsPerPeak = refPeaks is null ? null
                : refPeaks.SecondsPerPeak;
            int refTotalPeaks = refPeaks?.Min.Length ?? 0;
            float refCenterPeak = (float)(playPosition * refTotalPeaks);

            // Full-height downbeat guides — always on. Acts as a fixed yellow grid
            // so the user can eyeball alignment between decks at a glance.
            if (_downbeats is { Length: > 0 } && refSecondsPerPeak is double dbSpp)
            {
                // Vertical lines look fine without AA, and AA on N lines per frame
                // across two decks is a real cost on Skia's CPU rasteriser.
                _paints.Db.Color = _downbeatColor;
                var dbPaint = _paints.Db;
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
                _paints.BeatTick.Color = _live.BeatTick;
                // De-dupe against downbeats: a beat that's within ~1 ms of a
                // downbeat is the downbeat (already drawn full-height above).
                // Tolerance in seconds rather than rounded columns so this
                // never drifts off the downbeat line at high zoom.
                const double dedupeTol = 0.001;
                for (int i = 0; i < _beats.Length; i++)
                {
                    double bt = _beats[i];
                    bool isDownbeat = false;
                    if (_downbeats is { Length: > 0 })
                    {
                        foreach (var dt in _downbeats)
                        {
                            if (Math.Abs(dt - bt) < dedupeTol) { isDownbeat = true; break; }
                        }
                    }
                    else if ((i % 4) == 0) isDownbeat = true;
                    if (isDownbeat) continue;
                    // Use float positions — matches the downbeat math above so
                    // ticks and full-height lines stay perfectly aligned.
                    float beatCol = (float)(bt / btSpp);
                    float x = (float)((beatCol - refCenterPeak) / _playbackSpeed) + dstW / 2f;
                    if (x >= -2 && x < dstW + 2)
                        canvas.DrawLine(x, 0, x, 5, _paints.BeatTick);
                }
            }

            // Saved markers (memory cues) — amber vertical line + a small top flag,
            // using the same time→screen mapping as the grid so they track playback.
            if (_markerSecs is { Length: > 0 } && refSecondsPerPeak is double mkSpp)
            {
                var mk = _paints.Marker;
                // Hot pink — deliberately unlike the yellow downbeats and green vocal
                // lane so a saved cue jumps out. Dark contrast edge + a chunky top flag.
                var pink = _live.Marker;
                var edge = _live.Edge;
                foreach (var t in _markerSecs)
                {
                    float beatCol = (float)(t / mkSpp);
                    float x = (float)((beatCol - refCenterPeak) / _playbackSpeed) + dstW / 2f;
                    if (x < -8 || x >= dstW + 8) continue;
                    // 1px dark outline for contrast against bright waveforms.
                    mk.Style = SKPaintStyle.Stroke; mk.StrokeWidth = 5; mk.Color = edge;
                    canvas.DrawLine(x, 0, x, dstH, mk);
                    mk.StrokeWidth = 3; mk.Color = pink;
                    canvas.DrawLine(x, 0, x, dstH, mk);
                    // Chunky top flag so it's spottable at a glance.
                    mk.Style = SKPaintStyle.Fill;
                    mk.Color = edge; canvas.DrawRect(x - 7, 0, 15, 13, mk);
                    mk.Color = pink; canvas.DrawRect(x - 6, 0, 13, 11, mk);
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
                    _paints.Glow.Color = _live.Glow;
                    _paints.Glow.StrokeWidth = _isScrubbing ? 3 : 4;
                    var glow = _paints.Glow;
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
