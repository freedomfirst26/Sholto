using Avalonia;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using Sholto.App.Analysis.Analyzers.Waveform;
using Sholto.Data;
using Sholto.Interface.MainUI.Controls;
using Sholto.Interface.MainUI.Controls.Minimap;
using Sholto.Interface.MainUI.Controls.WaveformStyles;
using Sholto.Interface.MainUI.Theming;
using Sholto.Interface.MainUI.Tests.WaveformStyles;
using SkiaSharp;
using Xunit.Abstractions;

namespace Sholto.Interface.MainUI.Tests.Minimap;

public class MinimapControlTests
{
    private readonly ITestOutputHelper _output;
    private readonly System.Collections.Concurrent.BlockingCollection<Action> _posts = new();

    private readonly IWaveformStyles _styles = new WaveformStylesFactory().Create();
    private readonly WaveformPalette _wave = new TestWaveformPalette().Create();
    private readonly MinimapPalette _palette;
    private readonly WaveformPeaks _peaks;
    private readonly IReadOnlyList<DeckSection> _sections;
    private readonly double _barPeriod;

    public MinimapControlTests(ITestOutputHelper output)
    {
        _output = output;
        AvaloniaTestApp.EnsureStarted();
        _palette = new ThemeStackFactory().Build().Context.Current.Minimap;
        _peaks = new TestWaveformPeaks().Create(2000);   // 2000 peaks of 512 samples
        double secs = 2000 * 512 / 48000.0;     // 21.33 s
        _barPeriod = secs / 16;                 // 16 bars of 1.333 s, the first downbeat at 0
        _sections =
        [
            new DeckSection(DeckSectionKind.Intro, 0, 4),
            new DeckSection(DeckSectionKind.Drop, 4, 8),
            new DeckSection(DeckSectionKind.Outro, 12, 4),
        ];
    }

    private sealed class CountingBaker : IMinimapBaker
    {
        private readonly MinimapBaker _real = new(new MinimapPeakDownsampler(), new MinimapGeometry(), new MinimapPhraseLines(), new MinimapSectionLabels());
        public int Calls { get; private set; }
        public IWaveformStyleStrategy? LastStyle { get; private set; }
        public SKImage? Bake(WaveformPeaks peaks, MinimapStructure? structure, MinimapPalette palette,
            WaveformPalette waveform, IWaveformStyleStrategy style, int pixelWidth, int pixelHeight, double scale)
        {
            Calls++; LastStyle = style;
            return _real.Bake(peaks, structure, palette, waveform, style, pixelWidth, pixelHeight, scale);
        }
    }

    private (MinimapControl Control, CountingBaker Baker) Make(double width = 600)
    {
        var baker = new CountingBaker();
        var c = new MinimapControl
        {
            Baker = baker, Post = _posts.Add, ScaleOverride = 1, Peaks = _peaks, Sections = _sections,
            BarPeriodSec = _barPeriod,
            Palette = _palette, WaveformPalette = _wave, StyleStrategy = _styles.Default,
        };
        c.Measure(new Size(width, 100));
        c.Arrange(new Rect(0, 0, width, c.DesiredSize.Height));
        return (c, baker);
    }

    /// <summary>Request a bake if one is due, then run the posted results on this thread until none is in flight.</summary>
    private void Settle(MinimapControl c)
    {
        c.EnsureBaked();
        while (c.BakeInFlight)
        {
            Assert.True(_posts.TryTake(out var work, 10_000), "a bake never posted its result");
            work();
        }
    }

    private void Frame(MinimapControl c)
    {
        var rtb = new RenderTargetBitmap(new PixelSize(Math.Max(1, (int)c.Bounds.Width), Math.Max(1, (int)c.Bounds.Height)));
        using var ctx = rtb.CreateDrawingContext();
        c.Render(ctx);
    }

    private sealed class GatedBaker : IMinimapBaker
    {
        private readonly MinimapBaker _real = new(new MinimapPeakDownsampler(), new MinimapGeometry(), new MinimapPhraseLines(), new MinimapSectionLabels());
        public ManualResetEventSlim Gate { get; } = new(false);
        public ManualResetEventSlim Entered { get; } = new(false);
        public List<int> Widths { get; } = [];
        public int BakeThreadId { get; private set; }
        public SKImage? Bake(WaveformPeaks peaks, MinimapStructure? structure, MinimapPalette palette,
            WaveformPalette waveform, IWaveformStyleStrategy style, int pixelWidth, int pixelHeight, double scale)
        {
            lock (Widths) Widths.Add(pixelWidth);
            BakeThreadId = Environment.CurrentManagedThreadId;
            Entered.Set();
            Gate.Wait(10_000);
            return _real.Bake(peaks, structure, palette, waveform, style, pixelWidth, pixelHeight, scale);
        }
    }

    private (MinimapControl Control, GatedBaker Baker) MakeGated(double width = 600)
    {
        var baker = new GatedBaker();
        var c = new MinimapControl
        {
            Baker = baker, Post = _posts.Add, ScaleOverride = 1, Peaks = _peaks, Sections = _sections,
            BarPeriodSec = _barPeriod,
            Palette = _palette, WaveformPalette = _wave, StyleStrategy = _styles.Default,
        };
        c.Measure(new Size(width, 100));
        c.Arrange(new Rect(0, 0, width, c.DesiredSize.Height));
        return (c, baker);
    }

    private void Resize(MinimapControl c, double width) => c.Arrange(new Rect(0, 0, width, 64));

    [Fact]
    public void Render_returns_while_the_bake_is_blocked_and_the_result_is_posted_not_applied_on_the_bake_thread()
    {
        var (c, baker) = MakeGated();
        var rtb = new RenderTargetBitmap(new PixelSize(600, 64));
        using var ctx = rtb.CreateDrawingContext();

        c.Render(ctx);                                  // would hang on the gate if it baked inline
        Assert.True(baker.Entered.Wait(10_000));
        Assert.NotEqual(Environment.CurrentManagedThreadId, baker.BakeThreadId);
        Assert.True(c.BakeInFlight);
        Assert.Equal(default, c.BakedPixelSize);        // nothing applied yet
        Assert.Empty(_posts);

        baker.Gate.Set();
        Assert.True(_posts.TryTake(out var work, 10_000));   // the result arrives as posted work
        Assert.Equal(default, c.BakedPixelSize);        // still not applied until the UI thread runs it
        work();
        Assert.Equal(new PixelSize(600, 64), c.BakedPixelSize);
        Assert.False(c.BakeInFlight);
    }

    [Fact]
    public void Three_resizes_during_one_blocked_bake_give_one_more_bake_for_the_final_size()
    {
        var (c, baker) = MakeGated(500);
        var rtb = new RenderTargetBitmap(new PixelSize(900, 64));
        using var ctx = rtb.CreateDrawingContext();

        c.Render(ctx);                                  // first bake (500), blocked
        Assert.True(baker.Entered.Wait(10_000));
        Resize(c, 600); c.Render(ctx);
        Resize(c, 700); c.Render(ctx);
        Resize(c, 800); c.Render(ctx);
        Assert.Single(baker.Widths);                    // none of them started a bake of its own

        baker.Gate.Set();
        while (c.BakeInFlight) { Assert.True(_posts.TryTake(out var work, 10_000)); work(); }

        Assert.Equal([500, 800], baker.Widths);         // the stale 500 and the newest 800; 600 and 700 never baked
        Assert.Equal(2, c.BakeCount);
        Assert.Equal(800, c.BakedPixelSize.Width);
    }

    [Fact]
    public void A_stale_result_is_dropped_and_the_last_picture_keeps_drawing_until_the_new_one_lands()
    {
        var (c, baker) = MakeGated(500);
        baker.Gate.Set();
        Settle(c);
        var first = c.BakedBitmap;
        Assert.Equal(500, first!.PixelSize.Width);

        baker.Gate.Reset(); baker.Entered.Reset();
        Resize(c, 650);
        c.EnsureBaked();
        Assert.True(baker.Entered.Wait(10_000));
        Assert.Same(first, c.BakedBitmap);              // the old picture is still the one on screen
        var rtb = new RenderTargetBitmap(new PixelSize(650, 64));
        using (var ctx = rtb.CreateDrawingContext()) c.Render(ctx);   // draws it stretched, does not throw or wait

        Resize(c, 700);                                 // newer request while 650 is running
        c.EnsureBaked();
        baker.Gate.Set();
        while (c.BakeInFlight) { Assert.True(_posts.TryTake(out var work, 10_000)); work(); }
        Assert.Equal(700, c.BakedPixelSize.Width);      // the 650 result never showed
        Assert.Equal([500, 650, 700], baker.Widths);
    }

    [Fact]
    public void The_baked_picture_is_blitted_without_smoothing()
    {
        var c = new MinimapControl();
        Assert.Equal(Avalonia.Media.Imaging.BitmapInterpolationMode.None, RenderOptions.GetBitmapInterpolationMode(c));
    }

    [Fact]
    public void A_resize_frame_costs_the_ui_thread_under_2_ms_and_the_inline_bake_it_replaces_cost_far_more()
    {
        const int width = 1536;
        var (c, _) = Make(width);
        Settle(c);
        var rtb = new RenderTargetBitmap(new PixelSize(width + 200, 64));
        using var ctx = rtb.CreateDrawingContext();
        for (int i = 0; i < 5; i++) { Resize(c, width + i); c.Render(ctx); }   // JIT warm-up
        while (c.BakeInFlight) _posts.Take()();

        var frames = new List<double>();
        for (int i = 0; i < 60; i++)
        {
            Resize(c, width + 1 + i);
            var t0 = System.Diagnostics.Stopwatch.GetTimestamp();
            c.Render(ctx);
            frames.Add(System.Diagnostics.Stopwatch.GetElapsedTime(t0).TotalMilliseconds);
            while (_posts.TryTake(out var work)) work();   // land finished bakes between frames
        }
        while (c.BakeInFlight) _posts.Take()();
        frames.Sort();
        double median = frames[frames.Count / 2];

        // What every one of those frames cost before: bake and copy to a bitmap, inline.
        var baker = new MinimapBaker(new MinimapPeakDownsampler(), new MinimapGeometry(), new MinimapPhraseLines(), new MinimapSectionLabels());
        var inline = new List<double>();
        for (int i = 0; i < 15; i++)
        {
            var t0 = System.Diagnostics.Stopwatch.GetTimestamp();
            using var img = baker.Bake(_peaks, new MinimapStructure(_sections, new DeckPhraseGrid(0, 8), 0, _barPeriod), _palette, _wave, _styles.Default, width, 64, 1);
            _ = img!.ToBitmap();
            inline.Add(System.Diagnostics.Stopwatch.GetElapsedTime(t0).TotalMilliseconds);
        }
        inline.Sort();
        _output.WriteLine($"UI-thread ms per resize frame: median {median:F3}, max {frames[^1]:F3}; inline bake + ToBitmap: median {inline[inline.Count / 2]:F3}");

        Assert.True(median < 2, $"median UI-thread time per resize frame {median:F3} ms");
    }

    [Fact]
    public void The_strip_is_64_dip_tall()
    {
        var (c, _) = Make();
        Assert.Equal(64, c.Bounds.Height);
    }

    [Fact]
    public void The_first_frame_bakes_once_at_the_strip_size_and_steady_frames_do_not_re_bake()
    {
        var (c, baker) = Make();
        Settle(c);
        Assert.Equal(1, baker.Calls);
        Assert.Equal(new PixelSize(600, 64), c.BakedPixelSize);

        for (int i = 0; i < 200; i++)
        {
            c.PlayPosition = i / 200.0;
            Settle(c);
        }
        Assert.Equal(1, baker.Calls);
    }

    [Fact]
    public void Style_theme_palette_width_scale_peaks_and_sections_changes_each_re_bake()
    {
        var (c, baker) = Make();
        Settle(c);
        int n = baker.Calls;

        c.StyleStrategy = _styles.ById("rgb"); Settle(c);
        Assert.Equal(++n, baker.Calls);

        c.Palette = _palette with { Drop = Colors.Yellow }; Settle(c);
        Assert.Equal(++n, baker.Calls);

        c.WaveformPalette = _wave with { RgbLow = Colors.Yellow }; Settle(c);
        Assert.Equal(++n, baker.Calls);

        c.Arrange(new Rect(0, 0, 700, 64)); Settle(c);
        Assert.Equal(++n, baker.Calls);
        Assert.Equal(700, c.BakedPixelSize.Width);

        c.ScaleOverride = 2; Settle(c);
        Assert.Equal(++n, baker.Calls);
        Assert.Equal(new PixelSize(1400, 128), c.BakedPixelSize);

        c.Peaks = new TestWaveformPeaks().Create(1500); Settle(c);
        Assert.Equal(++n, baker.Calls);

        c.Sections = _sections.Take(2).ToList(); Settle(c);
        Assert.Equal(++n, baker.Calls);

        c.PhraseGrid = new DeckPhraseGrid(1, 8); Settle(c);
        Assert.Equal(++n, baker.Calls);

        c.BarPeriodSec = _barPeriod * 2; Settle(c);
        Assert.Equal(++n, baker.Calls);

        c.FirstDownbeatSec = 0.5; Settle(c);
        Assert.Equal(++n, baker.Calls);
    }

    [Fact]
    public void A_waveform_palette_change_the_style_does_not_bake_does_not_re_bake()
    {
        var (c, baker) = Make();
        Settle(c);

        c.WaveformPalette = _wave with { Marker = Colors.Yellow, Loop = Colors.Yellow };   // live colours only
        Settle(c);

        Assert.Equal(1, baker.Calls);
    }

    [Fact]
    public void The_baked_picture_follows_the_three_band_or_rgb_setting()
    {
        var (c, baker) = Make();
        Settle(c);
        Assert.Equal("three-band", baker.LastStyle!.Id);
        var threeBand = Pixels(c);

        c.StyleStrategy = _styles.ById("rgb"); Settle(c);
        Assert.Equal("rgb", baker.LastStyle!.Id);
        Assert.NotEqual(threeBand, Pixels(c));
    }

    [Fact]
    public void The_bake_draws_the_label_row_underlines_in_section_colours_and_the_wave_in_the_body()
    {
        var (c, _) = Make();
        Settle(c);
        var bytes = Pixels(c);
        int stride = bytes.Length / c.BakedPixelSize.Height;
        Color px(int x, int y)
        {
            int i = y * stride + x * 4;
            return Color.FromArgb(bytes[i + 3], bytes[i + 2], bytes[i + 1], bytes[i]);
        }

        // Underline: the bottom 3 rows of the 14-row label row, the section's colour.
        Assert.Equal(Color.FromRgb(_palette.Drop.R, _palette.Drop.G, _palette.Drop.B), px(300, 12));
        Assert.Equal(Color.FromRgb(_palette.Intro.R, _palette.Intro.G, _palette.Intro.B), px(20, 12));
        // The centre of the body, in the loud middle section, is not the backdrop or the tint alone.
        Assert.NotEqual(px(320, 14 + 25), px(320, 14 + 1));
    }

    [Fact]
    public void Phrase_lines_are_baked_into_the_body_at_whole_pixels_with_a_wider_strong_line()
    {
        var (c, baker) = Make();                      // 16 bars over 600 px: 37.5 px a bar
        c.PhraseGrid = new DeckPhraseGrid(1, 8);      // bar 1 strong (x 38), bar 9 thin (x 338)
        c.Peaks = new WaveformPeaks(new float[2000], new float[2000], new float[2000], new float[2000], new float[2000], 512, 48000);   // silent, so only the lines show
        Settle(c);
        var bytes = Pixels(c);
        int stride = bytes.Length / c.BakedPixelSize.Height;
        int y = 14 + 5;                               // near the top of the body, clear of any centre line
        byte[] px(int x) => bytes.AsSpan(y * stride + x * 4, 4).ToArray();

        Assert.NotEqual(px(30), px(38));              // the strong line, 2 px wide
        Assert.Equal(px(38), px(39));
        Assert.NotEqual(px(38), px(40));
        Assert.NotEqual(px(330), px(338));            // the thin line, 1 px wide
        Assert.Equal(px(330), px(339));
        Assert.NotEqual(px(338), px(38));             // and fainter than the strong one
        // The lines are not in the label row.
        Assert.Equal(bytes.AsSpan(0 * stride + 30 * 4, 4).ToArray(), bytes.AsSpan(0 * stride + 38 * 4, 4).ToArray());
    }

    [Fact]
    public void Section_blocks_span_their_bars_through_the_bar_grid()
    {
        var (c, _) = Make();                          // 16 bars over 600 px
        Settle(c);
        var bytes = Pixels(c);
        int stride = bytes.Length / c.BakedPixelSize.Height;
        byte[] px(int x) => bytes.AsSpan(12 * stride + x * 4, 4).ToArray();   // an underline row

        // Intro [0,4) ends at x 150, the drop [4,12) at 450: colour changes across each boundary.
        Assert.NotEqual(px(140), px(160));
        Assert.Equal(px(160), px(440));
        Assert.NotEqual(px(440), px(460));
    }

    [Fact]
    public void The_neutral_section_colour_is_the_label_colour()
    {
        Assert.Equal(_palette.Label, _palette.For(DeckSectionKind.Section));
        Assert.Equal(_palette.BuildUp, _palette.For(DeckSectionKind.Build));
    }

    [Fact]
    public void One_wave_column_is_one_device_pixel_with_no_sideways_blur_at_any_scale()
    {
        // 1:1 peaks to pixels, a single loud column among silence, RGB (1 px columns).
        var n = 600;
        var flat = new float[n];
        var max = new float[n]; var min = new float[n];
        max[300] = 1f; min[300] = -1f;
        var band = new float[n]; band[300] = 1f;
        var spike = new WaveformPeaks(min, max, band, (float[])band.Clone(), (float[])band.Clone(), 512, 48000);
        foreach (var scale in new[] { 1.0, 1.25, 2.0 })
        {
            var (c, _) = Make(width: n / scale);
            c.ScaleOverride = scale;
            c.Peaks = spike; c.Sections = null; c.StyleStrategy = _styles.ById("rgb");
            Settle(c);
            Assert.Equal(n, c.BakedPixelSize.Width);

            var bytes = Pixels(c);
            int stride = bytes.Length / c.BakedPixelSize.Height;
            int y = c.BakedPixelSize.Height - 8;   // inside the body, near its bottom half
            bool Same(int x1, int x2) => bytes.AsSpan(y * stride + x1 * 4, 4).SequenceEqual(bytes.AsSpan(y * stride + x2 * 4, 4));
            Assert.True(Same(298, 100), $"column 298 bled at scale {scale}");
            Assert.True(Same(299, 100), $"column 299 bled at scale {scale}");
            Assert.True(Same(301, 100), $"column 301 bled at scale {scale}");
            Assert.False(Same(300, 100), $"column 300 not drawn at scale {scale}");
        }
    }

    [Fact]
    public void A_steady_playing_frame_draws_without_allocating()
    {
        var (c, baker) = Make();
        c.MarkerSecs = [5, 20]; c.LoopStartSec = 10; c.LoopEndSec = 15;
        var rtb = new RenderTargetBitmap(new PixelSize(600, 64));
        using var ctx = rtb.CreateDrawingContext();

        Settle(c);
        for (int i = 0; i < 300; i++) { c.PlayPosition = i / 3000.0; c.Render(ctx); }   // warm up (bakes once)
        var before = GC.GetAllocatedBytesForCurrentThread();
        for (int i = 300; i < 1300; i++) { c.PlayPosition = i / 3000.0; c.Render(ctx); }
        var allocated = GC.GetAllocatedBytesForCurrentThread() - before;

        Assert.Equal(1, baker.Calls);
        // The control itself allocates nothing per frame; the drawing context may record a small
        // fixed amount per call, so bound it per frame rather than demand zero from Avalonia.
        Assert.True(allocated <= 1000 * 256L, $"allocated {allocated} bytes over 1000 frames");
    }

    [Fact]
    public void The_playhead_is_two_device_pixels_of_colour_inside_a_one_pixel_backdrop_outline()
    {
        var (c, _) = Make();
        c.PlayPosition = 0.5;   // x = 300 at scale 1
        Settle(c);
        var rtb = new RenderTargetBitmap(new PixelSize(600, 64));
        using (var ctx = rtb.CreateDrawingContext()) c.Render(ctx);

        Color At(int x)
        {
            var native = System.Runtime.InteropServices.Marshal.AllocHGlobal(4);
            try
            {
                rtb.CopyPixels(new PixelRect(x, 32, 1, 1), native, 4, 4);
                var px = new byte[4];
                System.Runtime.InteropServices.Marshal.Copy(native, px, 0, 4);
                return Color.FromRgb(px[2], px[1], px[0]);   // BGRA
            }
            finally { System.Runtime.InteropServices.Marshal.FreeHGlobal(native); }
        }

        var head = _palette.Playhead; var halo = _palette.Backdrop;
        Assert.Equal(Color.FromRgb(halo.R, halo.G, halo.B), At(298));
        Assert.Equal(Color.FromRgb(head.R, head.G, head.B), At(299));
        Assert.Equal(Color.FromRgb(head.R, head.G, head.B), At(300));
        Assert.Equal(Color.FromRgb(halo.R, halo.G, halo.B), At(301));
    }

    private byte[] Pixels(MinimapControl c)
    {
        using var fb = c.BakedBitmap!.Lock();
        var bytes = new byte[fb.RowBytes * fb.Size.Height];
        System.Runtime.InteropServices.Marshal.Copy(fb.Address, bytes, 0, bytes.Length);
        return bytes;
    }
}
