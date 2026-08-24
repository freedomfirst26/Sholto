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

    // Vocal-presence rectangle colours — painted only where the vocals are. Green
    // when the vocal stem is audible, this flat grey when it's muted.
    public static readonly SKColor VocalActiveColor = new(0x34, 0xF0, 0x6F, 0xD0);   // VOX green
    public static readonly SKColor VocalInactiveColor = new(0x60, 0x64, 0x6B, 0xB0); // muted grey

    public static readonly StyledProperty<WaveformPeaks?> PeaksProperty =
        AvaloniaProperty.Register<WaveformControl, WaveformPeaks?>(nameof(Peaks));

    /// <summary>Mixed-track peaks used purely as the time→pixel reference for the
    /// scrolling beatgrid and live overlays. Stays populated even when every stem
    /// is muted, so the grid keeps moving while <see cref="Peaks"/> is empty.</summary>
    public static readonly StyledProperty<WaveformPeaks?> GridPeaksProperty =
        AvaloniaProperty.Register<WaveformControl, WaveformPeaks?>(nameof(GridPeaks));

    /// <summary>Vocal-presence regions (from <see cref="VocalRegionAnalyzer"/>). Not a
    /// waveform — the control paints one solid green rectangle per span on top of the
    /// band waveform, so you can see where the vocals sit at a glance. Null until
    /// stems land.</summary>
    public static readonly StyledProperty<VocalRegions?> VocalRegionsProperty =
        AvaloniaProperty.Register<WaveformControl, VocalRegions?>(nameof(VocalRegions));

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

    // Whether the channel gain has actually been measured. Until the FLX-4 fader is
    // touched we don't know it, so the gain line is not drawn (we never render an
    // unmeasured value). Default false.
    public static readonly StyledProperty<bool> GainKnownProperty =
        AvaloniaProperty.Register<WaveformControl, bool>(nameof(GainKnown), false);

    public static readonly StyledProperty<double> MagneticGlowSecProperty =
        AvaloniaProperty.Register<WaveformControl, double>(nameof(MagneticGlowSec), -1.0);

    public static readonly StyledProperty<bool> IsScrubbingProperty =
        AvaloniaProperty.Register<WaveformControl, bool>(nameof(IsScrubbing), false);

    public static readonly StyledProperty<WaveformPalette> PaletteProperty =
        AvaloniaProperty.Register<WaveformControl, WaveformPalette>(nameof(Palette), WaveformPalette.Bands);

    /// <summary>Loop-in point in seconds, or null = no loop. The control paints
    /// a translucent band between this and <see cref="LoopEndSec"/>.</summary>
    public static readonly StyledProperty<double?> LoopStartSecProperty =
        AvaloniaProperty.Register<WaveformControl, double?>(nameof(LoopStartSec));

    /// <summary>Loop-out point in seconds, or null = no loop.</summary>
    public static readonly StyledProperty<double?> LoopEndSecProperty =
        AvaloniaProperty.Register<WaveformControl, double?>(nameof(LoopEndSec));

    /// <summary>Theme-driven colour for the active-loop band. Alpha here drives
    /// the band's transparency over the beatgrid.</summary>
    public static readonly StyledProperty<Avalonia.Media.Color> LoopColorProperty =
        AvaloniaProperty.Register<WaveformControl, Avalonia.Media.Color>(
            nameof(LoopColor), Avalonia.Media.Color.FromArgb(0x55, 0xFF, 0xC7, 0x00));

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
    private WaveformPalette _bakedPalette;
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
        AffectsRender<WaveformControl>(GainOverlayColorProperty);
        AffectsRender<WaveformControl>(MagneticGlowSecProperty);
        AffectsRender<WaveformControl>(IsScrubbingProperty);
        PeaksProperty.Changed.AddClassHandler<WaveformControl>((c, _) => c.Rebake());
        PaletteProperty.Changed.AddClassHandler<WaveformControl>((c, _) => c.Rebake());
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
        AffectsRender<WaveformControl>(LoopColorProperty);
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

    public VocalRegions? VocalRegions
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

    public WaveformPalette Palette
    {
        get => GetValue(PaletteProperty);
        set => SetValue(PaletteProperty, value);
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

    public Avalonia.Media.Color LoopColor
    {
        get => GetValue(LoopColorProperty);
        set => SetValue(LoopColorProperty, value);
    }

    /// <summary>Cache the analyzer's spans in render-friendly form. The presence
    /// detection itself lives in <see cref="VocalRegionAnalyzer"/> — here we just
    /// project to (start, end) seconds and invalidate.</summary>
    private void OnVocalRegionsChanged()
    {
        var vr = VocalRegions;
        _vocalRegions = vr is null
            ? null
            : vr.Regions.Select(r => (r.StartSec, r.EndSec)).ToArray();
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

    /// <summary>The (low, mid, high) band colours for a palette. Shared by the
    /// waveform bake and the minimap so the section map follows the same theme.</summary>
    /// <summary>Attack/release envelope follower over a bin-height array: the value
    /// jumps up instantly when the signal rises (a sharp leading face at each kick)
    /// then decays geometrically by <paramref name="release"/> per bin (the bell's
    /// tail). This is what gives the Rekordbox "sideways bell where the flat face is
    /// the beat" — a symmetric blur would round the attack away instead.</summary>
    private static void AttackRelease(float[] h, float release)
    {
        float env = 0f;
        for (int i = 0; i < h.Length; i++)
        {
            float s = h[i];
            env = s > env ? s : env * release;   // instant attack, slow release
            h[i] = env;
        }
    }

    /// <summary>Fill one band's envelope as a single closed path, mirrored above and
    /// below the centerline. The heights are sampled at bin centers so the fill is a
    /// smooth silhouette rather than a stack of rectangles.</summary>
    private static void FillEnvelope(SKCanvas canvas, float[] h, int binPx, int width, float midY, SKPaint paint)
    {
        int n = h.Length;
        if (n == 0) return;
        float X(int b) => MathF.Min(width, b * binPx + binPx * 0.5f);

        using var path = new SKPath();
        path.MoveTo(0, midY - h[0]);
        for (int b = 0; b < n; b++) path.LineTo(X(b), midY - h[b]);
        path.LineTo(width, midY - h[n - 1]);
        path.LineTo(width, midY + h[n - 1]);
        for (int b = n - 1; b >= 0; b--) path.LineTo(X(b), midY + h[b]);
        path.LineTo(0, midY + h[0]);
        path.Close();
        canvas.DrawPath(path, paint);
    }

    /// <summary>When true, every theme draws the waveform in the Denon/Rekordbox
    /// 3-band scheme (low blue, mid orange, high white) regardless of palette — it
    /// reads far better for spotting the kick/beat than the per-theme hues. Flip to
    /// false to restore the per-theme colours below.</summary>
    private const bool ForceThreeBand = true;
    private static readonly (SKColor Low, SKColor Mid, SKColor High) ThreeBand =
        (new SKColor(0x2A, 0x7F, 0xFF), new SKColor(0xFF, 0x8C, 0x1A), new SKColor(0xF5, 0xF5, 0xFF));

    public static (SKColor Low, SKColor Mid, SKColor High) BandColors(WaveformPalette palette) =>
        ForceThreeBand ? ThreeBand : palette switch
    {
        WaveformPalette.Hot         => (new SKColor(0xFF, 0x3D, 0x3D), new SKColor(0x3D, 0xFF, 0x7A), new SKColor(0x3D, 0x8B, 0xFF)),
        WaveformPalette.Plasma      => (new SKColor(0x7C, 0x5C, 0xFF), new SKColor(0xFF, 0x4E, 0x9A), new SKColor(0x34, 0xF0, 0xC6)),
        WaveformPalette.Smoke       => (new SKColor(0x5A, 0x46, 0x36), new SKColor(0xE0, 0xA8, 0x60), new SKColor(0xF2, 0xE9, 0xD0)),
        WaveformPalette.Glacier     => (new SKColor(0x4C, 0x6B, 0x8A), new SKColor(0xEC, 0xF0, 0xF6), new SKColor(0xB4, 0x8E, 0xAD)),
        WaveformPalette.SubFocus    => (new SKColor(0x80, 0x14, 0x2E), new SKColor(0xFF, 0x1F, 0x3D), new SKColor(0xFF, 0xE5, 0xEA)),
        WaveformPalette.OctoberRust => (new SKColor(0x2D, 0x55, 0x12), new SKColor(0x69, 0xBE, 0x28), new SKColor(0xDC, 0xE6, 0xCF)),
        WaveformPalette.Massacre    => (new SKColor(0x5B, 0x4A, 0xE0), new SKColor(0xD4, 0x5C, 0xE0), new SKColor(0xF2, 0xDE, 0xFF)),
        WaveformPalette.Soule       => (new SKColor(0x2E, 0x47, 0x34), new SKColor(0x6A, 0x8F, 0x62), new SKColor(0xE8, 0xED, 0xE5)),
        WaveformPalette.BoardsOfCanada => (new SKColor(0x2A, 0x44, 0x52), new SKColor(0x7F, 0xB6, 0xC9), new SKColor(0xDD, 0xF0, 0xF2)),
        WaveformPalette.Pantera     => (new SKColor(0x7A, 0x3D, 0x22), new SKColor(0xFF, 0x6B, 0x2C), new SKColor(0xE0, 0xD8, 0xCC)),
        // Default = Denon/Rekordbox 3-band: low blue, mid orange, high white.
        _                           => (new SKColor(0x2A, 0x7F, 0xFF), new SKColor(0xFF, 0x8C, 0x1A), new SKColor(0xF5, 0xF5, 0xFF)),
    };

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

        // The three colours map to the three frequency band fields in
        // WaveformPeaks: Low (bass, innermost) / Mid / High (transients,
        // outermost). Contrast rule for every theme: no band may be so dark it
        // vanishes against the near-black background (the bass especially), and
        // adjacent bands must be separable by hue or luminance — otherwise the
        // waveform reads as one flat blob and you can't eyeball the arrangement.
        //
        // Bands: authentic Rekordbox — blue / white / yellow
        // Hot:    Serato — red / green / blue
        // Plasma: Sholto 2026 — violet / hot-pink / mint
        // Smoke:  warm whiskey — dark coal / saturated amber / pale cream
        // Glacier: nordic — slate-blue / frost-white / aurora-violet
        // SubFocus:    Nick Douwma's brand — deep crimson / signature bright red / pale rose
        // OctoberRust: Type O Negative album palette — deep forest / Pantone 369 C / bone
        // Massacre:    Birthday Massacre — indigo-violet bass / magenta-orchid mid /
        //              pale lilac highs. Keeps BM's purple identity but the bass is
        //              lifted off black so it reads instead of vanishing (the old
        //              deep-royal-purple bass disappeared against the background).
        // Soule:       Jeremy Soule / Skyrim — pine green / forest moss / snow white
        // BoardsOfCanada: dreamy 80s VHS — deep ocean teal / faded blue / pale cassette cream
        // Pantera:     Cowboys From Hell — dark rust / flame orange / bone
        //              (bass lifted from near-black charcoal so it reads on the deck)
        var (lowColor, midColor, highColor) = BandColors(palette);
        using var lowPaint  = new SKPaint { Color = lowColor,  Style = SKPaintStyle.Fill, IsAntialias = true };
        using var midPaint  = new SKPaint { Color = midColor,  Style = SKPaintStyle.Fill, IsAntialias = true };
        using var highPaint = new SKPaint { Color = highColor, Style = SKPaintStyle.Fill, IsAntialias = true };

        bool hasBands = peaks.Low.Length == width;

        // Per-band, track-calibrated references (each band vs its own 97th-percentile
        // across the track) so blue = real bass, not a proportion of the column.
        var scaling = hasBands
            ? WaveformBandScaling.Calibrate(peaks.Low, peaks.Mid, peaks.High)
            : default;
        // Nested stack (Rekordbox 3-band): the bands are drawn CUMULATIVELY — white
        // innermost, orange around it, blue outermost — so blue always CONTAINS
        // orange contains white; a loud mid/high can never paint over the bass.
        // Scale the summed height (low+mid+high) so the track's fullest column nearly
        // fills the half-height.
        float scale = hasBands ? midY * 0.95f / MathF.Max(scaling.MaxTotal, 1e-3f) : midY * 0.95f;

        // Coarse, smooth "sideways bell" envelope: peak-hold the columns into bins,
        // run an attack/release follower (sharp face on the beat, graceful decay),
        // then fill each band's cumulative envelope as a single anti-aliased path.
        // Bigger binPx = coarser / less detail.
        const int binPx = 3;             // bar width
        const float releaseCoef = 0.74f; // snappy release → each kick decays to a valley = distinct bells
        const float gate = 0.06f;        // drop bins quieter than this (kills between-kick fuzz)
        int bins = (width + binPx - 1) / binPx;

        if (!hasBands)
        {
            var amp = new float[bins];
            for (int b = 0; b < bins; b++)
            {
                if (ct.IsCancellationRequested) return null;
                int x0 = b * binPx, x1 = Math.Min(width, x0 + binPx);
                float a = 0f;
                for (int x = x0; x < x1; x++)
                    a = MathF.Max(a, MathF.Max(MathF.Abs(peaks.Max[x]), MathF.Abs(peaks.Min[x])));
                amp[b] = a * midY;
            }
            AttackRelease(amp, releaseCoef);
            FillEnvelope(canvas, amp, binPx, width, midY, lowPaint);
            return surface.Snapshot();
        }

        var lowH = new float[bins];
        var midH = new float[bins];
        var highH = new float[bins];
        for (int b = 0; b < bins; b++)
        {
            if (ct.IsCancellationRequested) return null;
            int x0 = b * binPx, x1 = Math.Min(width, x0 + binPx);
            float ml = 0f, mm = 0f, mh = 0f;
            for (int x = x0; x < x1; x++)
            {
                var (nl, nm, nh) = scaling.Normalize(peaks.Low[x], peaks.Mid[x], peaks.High[x]);
                if (nl > ml) ml = nl;
                if (nm > mm) mm = nm;
                if (nh > mh) mh = nh;
            }
            // Gate the quiet stuff so only real transients survive (less clutter,
            // sharper kicks) — then scale.
            lowH[b]  = ml < gate ? 0f : ml * scale;
            midH[b]  = mm < gate ? 0f : mm * scale;
            highH[b] = mh < gate ? 0f : mh * scale;
        }

        AttackRelease(lowH,  releaseCoef);
        AttackRelease(midH,  releaseCoef);
        AttackRelease(highH, releaseCoef);

        // Cumulative edges: white core [0..high], orange out to [+mid], blue out to
        // [+low]. Draw outermost (blue) first so each inner band paints over the
        // centre and the outer bands survive as rings — blue ⊃ orange ⊃ white.
        var midE = new float[bins];
        var lowE = new float[bins];
        for (int b = 0; b < bins; b++)
        {
            midE[b] = MathF.Min(midY, highH[b] + midH[b]);
            lowE[b] = MathF.Min(midY, highH[b] + midH[b] + lowH[b]);
        }

        FillEnvelope(canvas, lowE,  binPx, width, midY, lowPaint);   // blue  — outer ring
        FillEnvelope(canvas, midE,  binPx, width, midY, midPaint);   // orange — middle ring
        FillEnvelope(canvas, highH, binPx, width, midY, highPaint);  // white — core

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
        var loopColor = new SKColor(LoopColor.R, LoopColor.G, LoopColor.B, LoopColor.A);
        context.Custom(new BlitOperation(new Rect(Bounds.Size), _baked, _bakedFor, GridPeaks, PlayPosition, PlaybackSpeed, GainOverlay, GainKnown, MagneticGlowSec, IsScrubbing, BeatTimes, DownbeatTimes, LoopStartSec, LoopEndSec, downbeatColor, gainColor, loopColor, _vocalRegions, VocalsActive, MarkerSecs));

        // Grid-edit mode: red border tint so the user knows clicks set anchors.
        if (GridEditMode)
        {
            var pen = new Avalonia.Media.Pen(new Avalonia.Media.SolidColorBrush(
                Avalonia.Media.Color.FromArgb(0xE0, 0xE5, 0x39, 0x35)), 2);
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

        double secondsPerPeak = refPeaks.SamplesPerPeak / (double)AudioFileDecoder.TargetSampleRate;
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
        [ThreadStatic] private static SKPaint? _loopPaint;
        [ThreadStatic] private static SKPaint? _vocalPaint;
        [ThreadStatic] private static SKPaint? _markerPaint;

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

        public BlitOperation(Rect bounds, SKImage? image, WaveformPeaks? peaks, WaveformPeaks? gridPeaks, double playPosition, double playbackSpeed, double gain, bool gainKnown, double magneticGlowSec, bool isScrubbing, double[]? beats, double[]? downbeats, double? loopStartSec, double? loopEndSec, SKColor downbeatColor, SKColor gainColor, SKColor loopColor, (double Start, double End)[]? vocalRegions, bool vocalsActive, double[]? markerSecs)
        {
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
                    float vcenter = (float)(_playPosition * vtotal);
                    double vspp = vref.SamplesPerPeak / (double)AudioFileDecoder.TargetSampleRate;
                    _vocalPaint ??= new SKPaint { Style = SKPaintStyle.Fill, IsAntialias = false };
                    // Green when the vocal stem is audible, grey when it's muted — so
                    // muting VOX greys out the sections where the vocals are.
                    _vocalPaint.Color = _vocalsActive ? VocalActiveColor : VocalInactiveColor;
                    const float bandH = 6f; // a thick line centred in the lane, not a tall block
                    float y0 = (dstH - bandH) / 2f;

                    foreach (var (s, e) in _vocalRegions)
                    {
                        // Column-index → screen-x (same transform as the waveform blit/grid).
                        float x0 = (float)(((s / vspp) - vcenter) / _playbackSpeed) + dstW / 2f;
                        float x1 = (float)(((e / vspp) - vcenter) / _playbackSpeed) + dstW / 2f;
                        if (x1 <= 0 || x0 >= dstW) continue;
                        float cx0 = Math.Max(0, x0), cx1 = Math.Min(dstW, x1);
                        canvas.DrawRect(cx0, y0, cx1 - cx0, bandH, _vocalPaint);
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
                double lpSpp = loopTimeRef.SamplesPerPeak / (double)AudioFileDecoder.TargetSampleRate;
                int totalPeaks = loopTimeRef.Min.Length;
                float lpCenter = (float)(_playPosition * totalPeaks);
                float xStart = (float)(((loopS / lpSpp) - lpCenter) / _playbackSpeed) + dstW / 2f;
                float xEnd   = (float)(((loopE / lpSpp) - lpCenter) / _playbackSpeed) + dstW / 2f;
                if (xEnd > 0 && xStart < dstW)
                {
                    float x0 = Math.Max(0, xStart);
                    float x1 = Math.Min(dstW, xEnd);
                    _loopPaint ??= new SKPaint { Style = SKPaintStyle.Fill, IsAntialias = false };
                    _loopPaint.Color = _loopColor;
                    canvas.DrawRect(x0, 0, x1 - x0, dstH, _loopPaint);

                    // Bright edge stripes at loop-in and loop-out — full-opacity
                    // accent so the region reads as a clearly bounded box rather
                    // than a vague tint. Only draws the edge if it's actually
                    // on-screen (clipped sides skip it).
                    var edgeColor = new SKColor(_loopColor.Red, _loopColor.Green, _loopColor.Blue, 0xFF);
                    _loopPaint.Color = edgeColor;
                    _loopPaint.Style = SKPaintStyle.Stroke;
                    _loopPaint.StrokeWidth = 2;
                    if (xStart >= 0)     canvas.DrawLine(xStart, 0, xStart, dstH, _loopPaint);
                    if (xEnd   <= dstW)  canvas.DrawLine(xEnd,   0, xEnd,   dstH, _loopPaint);
                    _loopPaint.Style = SKPaintStyle.Fill; // restore for next frame
                }
            }

            _headPaint ??= new SKPaint { Color = SKColors.White, StrokeWidth = 2, IsAntialias = false };
            int halfX = dstW / 2;
            canvas.DrawLine(halfX, 0, halfX, dstH, _headPaint);

            // Gain overlay: a thin horizontal line where Y = 0 means 100% (top) and
            // Y = dstH means 0%. So gain=1 → top, gain=0 → bottom. Drawn only once the
            // channel fader has been measured — an unmeasured level is not rendered.
            if (_gainKnown)
            {
                float gainY = (float)((1.0 - Math.Clamp(_gain, 0, 1)) * dstH);
                _gainPaint ??= new SKPaint { StrokeWidth = 1, IsAntialias = false };
                _gainPaint.Color = _gainColor;
                canvas.DrawLine(0, gainY, dstW, gainY, _gainPaint);
            }

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
                        canvas.DrawLine(x, 0, x, 5, _beatTickPaint);
                }
            }

            // Saved markers (memory cues) — amber vertical line + a small top flag,
            // using the same time→screen mapping as the grid so they track playback.
            if (_markerSecs is { Length: > 0 } && refSecondsPerPeak is double mkSpp)
            {
                _markerPaint ??= new SKPaint { IsAntialias = false };
                var mk = _markerPaint;
                // Hot pink — deliberately unlike the yellow downbeats and green vocal
                // lane so a saved cue jumps out. Dark contrast edge + a chunky top flag.
                var pink = new SKColor(0xFF, 0x2D, 0x95, 0xFF);
                var edge = new SKColor(0x00, 0x00, 0x00, 0x99);
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
